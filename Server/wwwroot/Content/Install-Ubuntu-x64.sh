#!/bin/bash
HostName=
Organization=
GUID=$(cat /proc/sys/kernel/random/uuid)
UpdatePackagePath=""
InstallDir="/usr/local/bin/Pronetsys"

apt-get update
apt-get -y install curl

ETag=$(curl --head $HostName/Content/Pronetsys-Linux.zip | grep -i "etag" | cut -d' ' -f 2)
LogPath="/var/log/pronetsys/Agent_Install.log"

mkdir -p /var/log/pronetsys
Args=( "$@" )
ArgLength=${#Args[@]}

for (( i=0; i<${ArgLength}; i+=2 ));
do
    if [ "${Args[$i]}" = "--uninstall" ]; then
        systemctl stop pronetsys-agent
        rm -r -f $InstallDir
        rm -f /etc/systemd/system/pronetsys-agent.service
        systemctl daemon-reload
        exit
    elif [ "${Args[$i]}" = "--path" ]; then
        UpdatePackagePath="${Args[$i+1]}"
    fi
done

if [ -z "$ETag" ]; then
    echo  "ETag is empty.  Aborting install." | tee -a $LogPath
    exit 1
fi

UbuntuVersion=$(lsb_release -r -s)

wget -q https://packages.microsoft.com/config/ubuntu/$UbuntuVersion/packages-microsoft-prod.deb -O packages-microsoft-prod.deb
dpkg -i packages-microsoft-prod.deb
apt-get update
apt-get -y install apt-transport-https
apt-get -y install dotnet-runtime-8.0
rm packages-microsoft-prod.deb

apt-get -y install libx11-dev
apt-get -y install libxrandr-dev
apt-get -y install unzip
apt-get -y install libc6-dev
apt-get -y install libxtst-dev
apt-get -y install xclip
apt-get -y install jq


if [ -f "$InstallDir/ConnectionInfo.json" ]; then
    SavedGUID=`cat "$InstallDir/ConnectionInfo.json" | jq -r '.DeviceID'`
     if [[ "$SavedGUID" != "null" && -n "$SavedGUID" ]]; then
        GUID="$SavedGUID"
    fi
fi

rm -r -f $InstallDir
rm -f /etc/systemd/system/pronetsys-agent.service

mkdir -p $InstallDir

if [ -z "$UpdatePackagePath" ]; then
    echo  "Downloading client." | tee -a $LogPath
    wget -q -O /tmp/Pronetsys-Linux.zip $HostName/Content/Pronetsys-Linux.zip
else
    echo  "Copying install files." | tee -a $LogPath
    cp "$UpdatePackagePath" /tmp/Pronetsys-Linux.zip
    rm -f "$UpdatePackagePath"
fi

unzip -o /tmp/Pronetsys-Linux.zip -d $InstallDir
rm -f /tmp/Pronetsys-Linux.zip
chmod +x $InstallDir/Pronetsys_Agent
chmod +x $InstallDir/Desktop/Pronetsys_Desktop


connectionInfo="{
    \"DeviceID\":\"$GUID\", 
    \"Host\":\"$HostName\",
    \"OrganizationID\": \"$Organization\",
    \"ServerVerificationToken\":\"\"
}"

echo "$connectionInfo" > $InstallDir/ConnectionInfo.json

curl --head $HostName/Content/Pronetsys-Linux.zip | grep -i "etag" | cut -d' ' -f 2 > $InstallDir/etag.txt

echo Creating service. | tee -a $LogPath

serviceConfig="[Unit]
Description=The Pronetsys agent used for remote access.

[Service]
WorkingDirectory=$InstallDir
ExecStart=$InstallDir/Pronetsys_Agent
Restart=always
StartLimitIntervalSec=0
RestartSec=10

[Install]
WantedBy=graphical.target"

echo "$serviceConfig" > /etc/systemd/system/pronetsys-agent.service

systemctl enable pronetsys-agent
systemctl restart pronetsys-agent

echo Install complete. | tee -a $LogPath
