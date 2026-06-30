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

package kr.or.kashi.hde.session;

import android.util.Log;

import java.io.IOException;
import java.io.InputStream;
import java.io.OutputStream;
import java.lang.ref.WeakReference;
import java.util.ArrayDeque;

/** @hide */
public class SessionAdapterDelegate implements NetworkSession {
    private static final String TAG = SessionAdapterDelegate.class.getSimpleName();
    private static final int MAX_RX_QUEUE_SIZE = 32 * 1024;

    private final WeakReference<NetworkSessionAdapter> mAdapter;
    private ReadStream mInputStream;
    private WriteStream mOutputStream;

    public SessionAdapterDelegate(NetworkSessionAdapter adapter) {
        mAdapter = new WeakReference<>(adapter);
    }

    public void putData(byte[] b) {
        if (mInputStream != null) {
            mInputStream.addBuffer(b);
        }
    }

    @Override
    public boolean open() {
        mInputStream = new ReadStream();
        mOutputStream = new WriteStream();
        return true;
    }

    @Override
    public void close() {
        try {
            if (mInputStream != null) {
                mInputStream.close();
                mInputStream = null;
            }
            if (mOutputStream != null) {
                mOutputStream.close();
                mOutputStream = null;
            }
        } catch (IOException e) {
            e.printStackTrace();
        }
    }

    @Override
    public InputStream getInputStream() {
        return mInputStream;
    }

    @Override
    public OutputStream getOutputStream() {
        return mOutputStream;
    }

    private class ReadStream extends InputStream {
        private final ArrayDeque<byte[]> mQueue = new ArrayDeque<>();
        private int mQueueSize = 0;
        private int mReadOffset = 0;
        private boolean mClosed = false;

        public void addBuffer(byte[] b) {
            if (b == null || b.length == 0) return;

            byte[] copy = new byte[b.length];
            System.arraycopy(b, 0, copy, 0, b.length);

            synchronized (mQueue) {
                if (mClosed) return;

                while (!mQueue.isEmpty() && (mQueueSize + copy.length) > MAX_RX_QUEUE_SIZE) {
                    byte[] removed = mQueue.poll();
                    if (removed != null) {
                        int removedBytes = removed.length;
                        if (mReadOffset > 0) {
                            removedBytes -= mReadOffset;
                            mReadOffset = 0;
                        }
                        mQueueSize -= removedBytes;
                    }
                }

                if ((mQueueSize + copy.length) > MAX_RX_QUEUE_SIZE) {
                    Log.w(TAG, "drop oversized RX buffer len=" + copy.length);
                    return;
                }

                mQueue.add(copy);
                mQueueSize += copy.length;
                mQueue.notifyAll();
            }
        }

        private boolean waitForDataLocked() throws IOException {
            while (!mClosed && mQueueSize == 0) {
                try {
                    mQueue.wait();
                } catch (InterruptedException e) {
                    throw new IOException("interrupted while waiting RX data", e);
                }
            }
            return mQueueSize > 0;
        }

        @Override
        public int read() throws IOException {
            synchronized (mQueue) {
                if (!waitForDataLocked()) return -1;

                byte[] head = mQueue.peek();
                int value = head[mReadOffset] & 0xFF;
                mReadOffset++;
                mQueueSize--;

                if (mReadOffset >= head.length) {
                    mQueue.poll();
                    mReadOffset = 0;
                }
                return value;
            }
        }

        @Override
        public int read(byte b[], int off, int len) throws IOException {
            if (b == null) throw new NullPointerException("buffer == null");
            if (off < 0 || len < 0 || off + len > b.length) {
                throw new IndexOutOfBoundsException("off=" + off + ", len=" + len + ", size=" + b.length);
            }
            if (len == 0) return 0;

            synchronized (mQueue) {
                if (!waitForDataLocked()) return -1;

                int copied = 0;
                while (copied < len && mQueueSize > 0) {
                    byte[] head = mQueue.peek();
                    int availableInHead = head.length - mReadOffset;
                    int copyLen = Math.min(len - copied, availableInHead);
                    System.arraycopy(head, mReadOffset, b, off + copied, copyLen);

                    copied += copyLen;
                    mReadOffset += copyLen;
                    mQueueSize -= copyLen;

                    if (mReadOffset >= head.length) {
                        mQueue.poll();
                        mReadOffset = 0;
                    }
                }
                return copied;
            }
        }

        @Override
        public long skip(long n) throws IOException {
            if (n <= 0L) return 0L;
            synchronized (mQueue) {
                if (!waitForDataLocked()) return 0L;

                long skipped = 0;
                while (skipped < n && mQueueSize > 0) {
                    byte[] head = mQueue.peek();
                    int availableInHead = head.length - mReadOffset;
                    int skipLen = (int)Math.min(n - skipped, availableInHead);
                    mReadOffset += skipLen;
                    mQueueSize -= skipLen;
                    skipped += skipLen;

                    if (mReadOffset >= head.length) {
                        mQueue.poll();
                        mReadOffset = 0;
                    }
                }
                return skipped;
            }
        }

        @Override
        public int available() throws IOException {
            synchronized (mQueue) {
                return mQueueSize;
            }
        }

        @Override
        public void close() throws IOException {
            synchronized (mQueue) {
                mClosed = true;
                mQueue.clear();
                mQueueSize = 0;
                mReadOffset = 0;
                mQueue.notifyAll();
            }
        }
    }

    private class WriteStream extends OutputStream {
        public WriteStream() { }

        @Override
        public void write(int b) throws IOException {
            onWriteBytes(new byte[] { ((byte)(b & 0xFF)) });
        }

        @Override
        public void write(byte b[], int off, int len) throws IOException {
            if (b == null) throw new NullPointerException("buffer == null");
            if (off < 0 || len < 0 || off + len > b.length) {
                throw new IndexOutOfBoundsException("off=" + off + ", len=" + len + ", size=" + b.length);
            }
            if (len == 0) return;

            if (b.length == len && off == 0) {
                onWriteBytes(b);
            } else {
                byte[] data = new byte[len];
                System.arraycopy(b, off, data, 0, len);
                onWriteBytes(data);
            }
        }

        private void onWriteBytes(byte[] b) {
            NetworkSessionAdapter adapter = mAdapter.get();
            if (adapter != null) {
                adapter.onWrite(b);
            }
        }
    }
}
