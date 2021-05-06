#!/bin/bash

set -e
pwd=$(pwd)

version=$(xmllint --xpath "string(//Project/PropertyGroup/Version)" ../src/MaintenanceTimersPlugin.csproj)
signkey=5E90FAE850ECE85E1B3096801101D2C8B78BAD31
pkgdir=$(pwd)/../..

export DOTNET_SKIP_FIRST_TIME_EXPERIENCE=true

echo "Building Debug configuration (version $version),,,"
echo "- Building package..."
rm -rf /tmp/maintenancetimersplugin
cd $pwd/../src
mkdir -p /tmp/maintenancetimersplugin/maintenancetimersplugin_$version/opt/dsf/bin
dotnet publish -r linux-arm -c Debug -o /tmp/maintenancetimersplugin/maintenancetimersplugin_$version/opt/dsf/bin

echo "- Arranging files..."
cp -r $pwd/DEBIAN /tmp/maintenancetimersplugin/maintenancetimersplugin_$version/DEBIAN
cp -r $pwd/opt /tmp/maintenancetimersplugin/maintenancetimersplugin_$version/
cp -r $pwd/usr /tmp/maintenancetimersplugin/maintenancetimersplugin_$version/usr
sed -i "s/VERSION/$version/g" /tmp/maintenancetimersplugin/maintenancetimersplugin_$version/opt/dsf/plugins/MaintenanceTimers.json
sed -i "s/VERSION/$version/g" /tmp/maintenancetimersplugin/maintenancetimersplugin_$version/DEBIAN/control
sed -i "s/VERSION/$version/g" /tmp/maintenancetimersplugin/maintenancetimersplugin_$version/DEBIAN/changelog

echo "- Packaging files..."
cd /tmp/maintenancetimersplugin
dpkg-deb --build maintenancetimersplugin_$version
dpkg-sig -k $signkey -s builder maintenancetimersplugin_$version.deb
mv /tmp/maintenancetimersplugin/maintenancetimersplugin_$version.deb $pkgdir/maintenancetimersplugin_$version.deb
#rm -rf /tmp/maintenancetimersplugin/maintenancetimersplugin_$version
