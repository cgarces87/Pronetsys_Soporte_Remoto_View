#!/bin/bash

HostName=
Organization=
GUID="$(uuidgen)"
UpdatePackagePath=""
InstallDir="/usr/local/bin/Pronetsys"
ETag=$(curl --head $HostName/Content/Pronetsys-MacOS-arm64.zip | grep -i "etag" | cut -d' ' -f 2)
LogPath="/var/log/pronetsys/Agent_Install.log"

mkdir -p /var/log/pronetsys

Args=( "$@" )
ArgLength=${#Args[@]}

for (( i=0; i<${ArgLength}; i+=2 ));
do
    if [ "${Args[$i]}" = "--uninstall" ]; then
        sudo launchctl bootout system /Library/LaunchDaemons/pronetsys-agent.plist
        rm -r -f $InstallDir/
        rm -f /Library/LaunchDaemons/pronetsys-agent.plist
        exit
    elif [ "${Args[$i]}" = "--path" ]; then
        UpdatePackagePath="${Args[$i+1]}"
    fi
done

if [ -z "$ETag" ]; then
    echo  "ETag is empty.  Aborting install." | tee -a $LogPath
    exit 1
fi


# Install Homebrew

if [[ -n "$SUDO_USER" && "$SUDO_USER" != "root" ]]; then
    su - $SUDO_USER -c '/bin/bash -c "$(curl -fsSL https://raw.githubusercontent.com/Homebrew/install/HEAD/install.sh)"'
fi

Owner=$(ls -l /usr/local/bin/brew | awk '{print $3}')

su - $Owner -c "brew update"

# Install other dependencies
su - $Owner -c "brew install curl"
su - $Owner -c "brew install jq"


if [ -f "$InstallDir/ConnectionInfo.json" ]; then
    SavedGUID=`su - $Owner -c "cat '$InstallDir/ConnectionInfo.json' | jq -r '.DeviceID'"`
    if [[ "$SavedGUID" != "null" && -n "$SavedGUID" ]]; then
        GUID="$SavedGUID"
    fi
fi

rm -r -f /Applications/Pronetsys
rm -f /Library/LaunchDaemons/pronetsys-agent.plist

mkdir -p $InstallDir
chmod -R 755 $InstallDir

if [ -z "$UpdatePackagePath" ]; then
    echo  "Downloading client..." >> /tmp/Pronetsys_Install.log
    curl $HostName/Content/Pronetsys-MacOS-arm64.zip --output $InstallDir/Pronetsys-MacOS-arm64.zip
else
    echo  "Copying install files..." >> /tmp/Pronetsys_Install.log
    cp "$UpdatePackagePath" $InstallDir/Pronetsys-MacOS-arm64.zip
    rm -f "$UpdatePackagePath"
fi

unzip -o $InstallDir/Pronetsys-MacOS-arm64.zip -d $InstallDir
rm -f $InstallDir/Pronetsys-MacOS-arm64.zip
chmod +x $InstallDir/Pronetsys_Agent
chmod +x $InstallDir/Desktop/Pronetsys_Desktop

connectionInfo="{
    \"DeviceID\":\"$GUID\", 
    \"Host\":\"$HostName\",
    \"OrganizationID\": \"$Organization\",
    \"ServerVerificationToken\":\"\"
}"

echo "$connectionInfo" > $InstallDir/ConnectionInfo.json

curl --head $HostName/Content/Pronetsys-MacOS-arm64.zip | grep -i "etag" | cut -d' ' -f 2 > $InstallDir/etag.txt


plistFile="<?xml version=\"1.0\" encoding=\"UTF-8\"?>
<!DOCTYPE plist PUBLIC \"-//Apple//DTD PLIST 1.0//EN\" \"http://www.apple.com/DTDs/PropertyList-1.0.dtd\">
<plist version=\"1.0\">
<dict>
    <key>Label</key>
    <string>com.pronetsys.agent</string>
    <key>ProgramArguments</key>
    <array>
        <string>$InstallDir/Pronetsys_Agent</string>
    </array>
    <key>KeepAlive</key>
    <true/>
</dict>
</plist>"
echo "$plistFile" > "/Library/LaunchDaemons/pronetsys-agent.plist"


sudo launchctl bootstrap system /Library/LaunchDaemons/pronetsys-agent.plist
sudo launchctl kickstart -k system/com.pronetsys.agent