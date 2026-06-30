/*
 * Copyright (C) 2023 Korea Association of AI Smart Home.
 * Copyright (C) 2023 KyungDong Navien Co, Ltd.
 *
 * Licensed under the Apache License, Version 2.0 (the "License");
 * you may not use this file except in compliance with the License.
 * You may obtain a copy of the License at
 *
 *      http://www.apache.org/licenses/LICENSE-2.0
 *
 * Unless required by applicable law or agreed to in writing, software
 * distributed under the License is distributed on an "AS IS" BASIS,
 * WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
 * See the License for the specific language governing permissions and
 * limitations under the License.
 */

package kr.or.kashi.hde;

import android.os.Handler;
import android.util.Log;

import java.io.File;
import java.io.IOException;
import java.io.RandomAccessFile;
import java.util.Arrays;
import java.util.List;
import java.util.concurrent.TimeUnit;

/**
 * Sends the board-MCU watchdog-disable command once when the emulator app starts.
 *
 * Site-Hub <-> T5 TTL-232 frame:
 *   STX, OP-code, DATA, BCC, ETX
 *   BCC = OP-code xor DATA
 *
 * Watchdog 기능 x command:
 *   STX=0x02, OP=0x55, DATA=0x00, BCC=0x55, ETX=0x03
 */
public final class McuWatchdogDisabler {
    private static final String TAG = "McuWatchdogDisabler";

    private static final String WATCHDOG_UART_PORT = "/dev/ttyS5";
    private static final int WATCHDOG_UART_BAUD = 115200;
    private static final int WATCHDOG_RX_TIMEOUT_MS = 5000;

    private static final byte STX = 0x02;
    private static final byte OP_WATCHDOG = 0x55;
    private static final byte DATA_DISABLE_WATCHDOG = 0x00;
    private static final byte ETX = 0x03;
    private static final byte[] WATCHDOG_DISABLE_PACKET = new byte[] {
            STX,
            OP_WATCHDOG,
            DATA_DISABLE_WATCHDOG,
            (byte) (OP_WATCHDOG ^ DATA_DISABLE_WATCHDOG),
            ETX
    };

    private static boolean sSentThisProcess;

    private McuWatchdogDisabler() {
    }

    public static synchronized void sendOnceOnAppStart(Handler handler) {
        if (sSentThisProcess) {
            Log.d(TAG, "watchdog-disable packet already sent in this process");
            return;
        }
        sSentThisProcess = true;

        Thread thread = new Thread(() -> sendInternal(), "MCU-Watchdog-Off");
        thread.start();
    }

    private static void sendInternal() {
        File portFile = new File(WATCHDOG_UART_PORT);
        Log.d(TAG, "watchdog UART candidate " + WATCHDOG_UART_PORT
                + " exists=" + portFile.exists()
                + ", canRead=" + portFile.canRead()
                + ", canWrite=" + portFile.canWrite());

        if (!portFile.exists()) {
            Log.e(TAG, "watchdog UART port does not exist: " + WATCHDOG_UART_PORT);
            return;
        }

        configureUartWithStty(WATCHDOG_UART_PORT, WATCHDOG_UART_BAUD);

        try (RandomAccessFile uart = new RandomAccessFile(portFile, "rw")) {
            // Drain stale bytes first so the RX log below represents the MCU response to this command.
            drainPendingBytes(uart);

            uart.write(WATCHDOG_DISABLE_PACKET);
            try {
                uart.getFD().sync();
            } catch (IOException e) {
                // TTY devices often do not support fd sync. write() already completed.
                Log.w(TAG, "watchdog UART fd sync unsupported; packet write completed");
            }

            Log.d(TAG, "watchdog-disable TX(" + WATCHDOG_UART_PORT + ", "
                    + WATCHDOG_UART_BAUD + "): " + bytesToHex(WATCHDOG_DISABLE_PACKET));

            readResponseOnce(uart);
        } catch (IOException | RuntimeException e) {
            Log.e(TAG, "watchdog-disable direct tty access failed", e);
        }
    }

    private static void drainPendingBytes(RandomAccessFile uart) {
        byte[] drainBuffer = new byte[128];
        int drained = 0;
        long deadline = System.currentTimeMillis() + 50L;
        try {
            while (System.currentTimeMillis() < deadline) {
                int read = uart.read(drainBuffer, 0, drainBuffer.length);
                if (read > 0) {
                    drained += read;
                    continue;
                }
                break;
            }
        } catch (IOException | RuntimeException ignored) {
            // Non-blocking tty read may throw on some vendor kernels. It is only a stale-byte drain.
        }
        if (drained > 0) {
            Log.d(TAG, "watchdog UART drained stale RX bytes=" + drained);
        }
    }

    private static void readResponseOnce(RandomAccessFile uart) {
        byte[] responseBuffer = new byte[128];
        int responseLength = 0;
        long deadline = System.currentTimeMillis() + WATCHDOG_RX_TIMEOUT_MS;

        while (System.currentTimeMillis() < deadline && responseLength < responseBuffer.length) {
            try {
                int read = uart.read(responseBuffer, responseLength,
                        responseBuffer.length - responseLength);
                if (read > 0) {
                    responseLength += read;
                    // The watchdog response is short. Give the MCU a small inter-byte window,
                    // then print the complete response as one log line.
                    try {
                        Thread.sleep(20);
                    } catch (InterruptedException e) {
                        Thread.currentThread().interrupt();
                        break;
                    }
                    continue;
                }
            } catch (IOException | RuntimeException e) {
                Log.w(TAG, "watchdog-disable RX read failed: " + e.getMessage());
                return;
            }

            try {
                Thread.sleep(10);
            } catch (InterruptedException e) {
                Thread.currentThread().interrupt();
                Log.w(TAG, "watchdog-disable RX wait interrupted");
                return;
            }
        }

        if (responseLength > 0) {
            byte[] response = Arrays.copyOf(responseBuffer, responseLength);
            Log.d(TAG, "watchdog-disable RX(" + WATCHDOG_UART_PORT + ", "
                    + WATCHDOG_UART_BAUD + "): " + bytesToHex(response));
        } else {
            Log.w(TAG, "watchdog-disable RX timeout/no data (" + WATCHDOG_RX_TIMEOUT_MS + "ms)");
        }
    }

    private static void configureUartWithStty(String port, int baud) {
        List<String[]> commands = Arrays.asList(
                new String[] {"/system/bin/stty", "-F", port, String.valueOf(baud), "raw", "-echo", "-echoe", "-echok", "min", "0", "time", "2"},
                new String[] {"/vendor/bin/stty", "-F", port, String.valueOf(baud), "raw", "-echo", "-echoe", "-echok", "min", "0", "time", "2"},
                new String[] {"/system/bin/toybox", "stty", "-F", port, String.valueOf(baud), "raw", "-echo", "-echoe", "-echok", "min", "0", "time", "2"},
                new String[] {"/vendor/bin/toybox", "stty", "-F", port, String.valueOf(baud), "raw", "-echo", "-echoe", "-echok", "min", "0", "time", "2"},
                new String[] {"stty", "-F", port, String.valueOf(baud), "raw", "-echo", "-echoe", "-echok", "min", "0", "time", "2"}
        );

        for (String[] command : commands) {
            if (runSttyCommand(command)) {
                Log.d(TAG, "watchdog UART configured: " + port + " " + baud
                        + " by " + command[0]);
                return;
            }
        }

        Log.w(TAG, "watchdog UART stty configure failed; writing packet directly to " + port);
    }

    private static boolean runSttyCommand(String[] command) {
        Process process = null;
        try {
            process = new ProcessBuilder(command)
                    .redirectErrorStream(true)
                    .start();
            boolean finished = process.waitFor(800, TimeUnit.MILLISECONDS);
            if (!finished) {
                process.destroy();
                return false;
            }
            return process.exitValue() == 0;
        } catch (IOException | InterruptedException | RuntimeException e) {
            if (e instanceof InterruptedException) {
                Thread.currentThread().interrupt();
            }
            return false;
        } finally {
            if (process != null) {
                process.destroy();
            }
        }
    }

    private static String bytesToHex(byte[] data) {
        StringBuilder builder = new StringBuilder();
        for (int i = 0; i < data.length; i++) {
            if (i > 0) builder.append(' ');
            builder.append(String.format("%02X", data[i] & 0xFF));
        }
        return builder.toString();
    }
}
