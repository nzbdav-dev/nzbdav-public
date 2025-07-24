# Demo
Click on the image below to play the gif

<img width="600" alt="dav-explorer" src="https://github.com/user-attachments/assets/35d28e6a-5bd9-4a3d-8e65-14e2b09a0f39" />

# Project Status

Very buggy and still in development. 
* UI is incomplete.
* No repair ability yet. Missing articles will cause problems.

# Features
✅ Webdav server  
✅ Mount NZB Documents  
✅ Unrar by default (compressed rars and password protected rars are not supported)  
✅ SabNZBD-compatible api for sonarr/radarr integration


# Quick Start

```
docker run --rm -it \
  -v $(pwd):/config \
  -e CONFIG_PATH=/config \
  -e USENET_HOST=news.newshosting.com \
  -e USENET_PORT=563 \
  -e USENET_USE_SSL=true \
  -e USENET_USER=abcdefg \
  -e USENET_PASS=abcdefg \
  -e USENET_CONNECTIONS=50 \
  -e API_KEY=abcdefghijklmnopqrstuvwxyz \
  -e WEBDAV_USER=abcdefg \
  -e WEBDAV_PASS=abcdefg \
  -e MOUNT_DIR=/mnt/nzbdav \
  -p 3000:3000 \
  ghcr.io/nzbdav-dev/nzbdav:pre-alpha
```

Since The UI is still a work in progress. Everything needs to be configured through environment variables.

| Config       | Description                                                                             |
| ------------ | --------------------------------------------------------------------------------------- |
| CONFIG_PATH  | Directory of where to store the database file                                           |
| API_KEY      | This is the api key you will configure in radarr/sonarr for sabnzbd integration         |
| WEBDAV_USER  | The username to access webdav. Important for rclone                                     |
| WEBDAV_PASS  | The password to access webdav. Important for rclone                                     |
| MOUNT_DIR    | The location at which you've mounted (or going to mount) the webdav root through rclone. Just set it to /tmp if testing and not using rclone at the moment. |


# RClone

Config

```
[nzb-dav]
type = webdav
url = // your endpoint
vendor = other
user = // your webdav user
pass = // your rclone-obscured password https://rclone.org/commands/rclone_obscure
```

Additional Cli-Args
```
--vfs-cache-mode=off
--buffer-size=1024
--dir-cache-time=1s
--links
```

* I just disable all rclone caching and stream directly..
* The end-client (Plex/VLC/Chrome webview/etc) will already buffer-ahead anyway

# More screenshots
<img width="300" alt="onboarding" src="https://github.com/user-attachments/assets/69fe332a-2c04-426e-9469-1655cef0d50e" />
<img width="300" alt="queue and history" src="https://github.com/user-attachments/assets/11b65734-48ed-4832-8ac6-4b403bbc2b14" />
<img width="300" alt="dav-explorer" src="https://github.com/user-attachments/assets/1948d455-5299-468f-8130-ec9df0e5efe6" />



