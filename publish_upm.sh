#!/bin/sh
# 获取当前分支
currentBranch=$(git symbolic-ref --short -q HEAD)

#单发模块
upmModule=("AndroidDependencyResolver")
githubRepoName=("android_dependency_resolver")

tag=$1
#是否正式发布，
publish2Release=$2

# 发布 UPM 脚本
publishUPM() {
    git tag -d $(git tag)
    
    git branch -D github_upm
    
    git subtree split --prefix=Assets/TapTap/AndroidDependencyResolver --branch github_upm
    
    git remote rm "$1"
    
    if [ $publish2Release = true ]; then
        echo "start push $1 to git@github.com:xd-platform/$3.git"
        git remote add "$1" git@github.com:xd-platform/"$3".git
    else
        echo "start push $1 to git@github.com:luckisnow/$3.git"  
        git remote add "$1" git@github.com:luckisnow/"$3".git
    fi;
    
    git checkout github_upm --force
    
    git tag "$2"
    
    git fetch --unshallow github_upm
    
    git push "AndroidDependencyResolver" github_upm --force --tags
    
    git checkout "$currentBranch" --force
        
}
for ((i=0;i<${#upmModule[@]};i++)); do
    publishUPM "${module[$i]}" "${upmModule[$i]}" "$tag" "${githubRepoName[$i]}" 
done