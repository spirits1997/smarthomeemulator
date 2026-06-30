build password android




    signingConfigs {
        debug {
            storeFile file("..\\platform.jks")
            storePassword "android"
            keyAlias "androiddebugkey"
            keyPassword "android"
        }

        release {
            storeFile file("..\\platform.jks")
            storePassword "android"
            keyAlias "androiddebugkey"
            keyPassword "android"
        }
    }