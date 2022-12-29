#!/bin/sh
rootPath=$(cd `dirname $0`; pwd)

function push() {
  
  echo email=qiankun@xd.com > .npmrc
  echo always-auth=true >> .npmrc
  echo registry=https://nexus.tapsvc.com/repository/npm-registry/ >> .npmrc
  echo //nexus.tapsvc.com/repository/npm-registry/:_authToken=NpmToken.de093789-9551-3238-a766-9d2c694f2600 >> .npmrc

  npm publish
  
  rm -rf .npmrc
  
  cd $rootPath
}
  
for file in $rootPath/Assets/TapTap/*
do 
  if test -d $file
  then 
    cd $file
    push
  fi
done

