# CloudflareDNSUpdate

## Basic configuration
Edit the included appsettings.json to update settings such as the update interval (CronExpression) and your Cloudflare token. You can also have it email you with failures and setup inclusion/exclusion lists, specify record type and some filters on which records to update.

Confused by those CronExpressions? I use: https://crontab.cronhub.io/

As for logging, I highly recommend that you change the logging path to an absolute location. Find the section "path": "Logs/log-.txt" and change it to something like "C:\\some\\path". Otherwise windows defaults the location to "C:\Windows\System32\Logs", which kind of sucks.

## Hangfire Console
The application used Hangfire to schedule the updates, by default you can browse the scheduled via http://127.0.0.1:5080/ using the username "admin" with the password "password". You can change the port and credentials in the appsettings.json.

<img width="1236" height="413" alt="image" src="https://github.com/user-attachments/assets/a80725a0-2059-426d-b0f6-ad4ffe43ba2e" />

## Install service

**CMD:** ```CloudflareDNSUpdate.exe install```

<img width="810" height="595" alt="image" src="https://github.com/user-attachments/assets/9bc8066d-1f81-4115-99b2-6c3258ebb4b6" />

## Remove Service

**CMD:** ```CloudflareDNSUpdate.exe uninstall```

<img width="817" height="428" alt="image" src="https://github.com/user-attachments/assets/36210225-362f-4417-9d55-f7a6eb5492e1" />
