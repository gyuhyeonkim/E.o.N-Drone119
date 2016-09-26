#!/bin/bash

###############################################################################
# SpiriGo dev environment installer for nVidia Jetson TK1 
# --------------------------------
# This script installs a SpiriGo system on a Jetson computer meant to be a
# companion computer on a flying ArduPilot quadcopter. DO NOT run this on
# any other systems as it is meant specifically for the Jetson.
#
# By default, this script will install the SpiriGo workspace in the current 
# user's home directory and all of its dependencies at the system level. 
#
# This script is meant to run from a fresh Grinch kernel. We have a Grinch 
# provision script but that one requires a reboot.
#
###############################################################################
set -e

###############################################################################
# Sanity Checks
###############################################################################
# the script will require sudo for some actions but cannot be invoked via 
# sudo mainly because ROS dependencies are finicky with permission
if [[ $EUID -eq 0 ]]; then
    echo "ERROR: Must be run WITHOUT sudo."
    exit 1
fi

# if you try and use this install script on another release you're gonna
# have a bad time.
source /etc/lsb-release
if [ "$DISTRIB_ID" != "Ubuntu" -o "$DISTRIB_RELEASE" != "14.04" ]; then
    echo "ERROR: Only Ubuntu 14.04 is supported."
    exit 1
fi

if (($(uname -r | grep grinch | wc -l) == 0)); then
    echo "ERROR: The Grinch kernel is not installed on your system."
    exit 1
fi

###############################################################################
# Variable Declaration
###############################################################################
# which user to install the code for; defaults to the user invoking this script
SPIRI_USER=${SPIRI_USER:-$USER}

# the root directory to base the install in. must exist already
SPIRI_HOME=${SPIRI_HOME:-/home/$SPIRI_USER}

# check that the data directory exists, if not create it
if !([ -d $SPIRI_HOME/temp ]); then
    mkdir -p $SPIRI_HOME/temp
fi
DATA=${DATA:-$SPIRI_HOME/temp}

###############################################################################
# Initial Configuration
###############################################################################
set -x

# set some warning colors 
export RED='\033[0;31m'
export GREEN='\033[0;32m'
export YELLOW='\033[0;33m'
export NO_COLOR='\033[0m'

# aptitude, git, and CUDA configuration
APTITUDE_OPTIONS="-y"
export DEBIAN_FRONTEND=noninteractive
export GIT_SSL_NO_VERIFY=1
export CUDA_VERSION="cuda-repo-l4t-r21.3-6-5-prod_6.5-42_armhf"
export OPENCV4TEGRA_VERSION="libopencv4tegra-repo_l4t-r21_2.4.10.1_armhf"

# do a hold here to prevent OpenGL from being overwritten
sudo apt-mark hold xserver-xorg-core

# add some ppas
sudo apt-add-repository universe
sudo apt-add-repository multiverse
sudo apt-add-repository restricted

# comment out EOL universe packages 
sudo cp /etc/apt/sources.list /etc/apt/sources.list.bak
sudo awk '/archive/ {$0="#"$0}1' /etc/apt/sources.list

# run apt update and get essential tools for the rest of the script
sudo apt-get update
sudo apt-get install $APTITUDE_OPTIONS dpkg git bash-completion command-not-found

###############################################################################
# Install CUDA 6.5
###############################################################################
# source: JetsonHacks

# check to see if CUDA 6.5 has already been installed
if (($(dpkg -l | grep cuda-toolkit-6-5 | wc -l) == 0))
then
    
    # check to see if the correct deb file has already been downloaded
    if [ -f $DATA/$CUDA_VERSION".deb" ]
    then
        CSUM="7652f8afcd01e5c82dc5c202a902469a"
        MD5=$(md5sum $DATA/$CUDA_VERSION".deb" | cut -d ' ' -f 1)
        if [ "$MD5" != "$CSUM" ]
        then
            sudo rm -f $CUDA_VERSION".deb"
            printf ${YELLOW}"Found invalid file "$CUDA_VERSION".deb. Program will attempt to download it again.\N"${NO_COLOR}
            wget http://developer.download.nvidia.com/embedded/L4T/r21_Release_v3.0/$CUDA_VERSION".deb" -P $DATA
        else
            printf ${GREEN}"Found valid file "$CUDA_VERSION".deb\n"${NO_COLOR}
        fi
    else
        wget http://developer.download.nvidia.com/embedded/L4T/r21_Release_v3.0/$CUDA_VERSION".deb" -P $DATA
    fi

    # download & install the actual CUDA Toolkit including the OpenGL toolkit from NVIDIA. 
    echo "Installing CUDA 6.5"
    sudo dpkg -i $DATA/$CUDA_VERSION".deb"
    sudo apt-get update
    sudo apt-get install $APTITUDE_OPTIONS cuda-toolkit-6-5

    # add yourself to the "video" group to allow access to the GPU
    sudo usermod -a -G video $SPIRI_USER

    # add the 32-bit CUDA paths to your .bashrc login script, and start using it in your current console:
    echo "# Add CUDA bin & library paths:" >> $SPIRI_HOME/.bashrc
    echo "export PATH=/usr/local/cuda-6.5/bin:$PATH" >> $SPIRI_HOME/.bashrc
    echo "export LD_LIBRARY_PATH=/usr/local/cuda-6.5/lib:$LD_LIBRARY_PATH" >> $SPIRI_HOME/.bashrc
    source $SPIRI_HOME/.bashrc
    printf ${GREEN}"CUDA 6.5 installed \n"${NO_COLOR}
else
    printf ${GREEN}"CUDA 6.5 is already installed on your system \n"${NO_COLOR}
fi

###############################################################################
# Install OpenCV4Tegra and configure it correctly
###############################################################################
# https://devtalk.nvidia.com/default/topic/835118/embedded-systems/incorrect-configuration-in-opencv4tegra-debian-packages-and-solution

# check to see if OpenCV4Tegra 2.4.10.1 has already been installed
# if not, install it
# if so, unhold it
if (($(dpkg -l | grep libopencv4tegra | wc -l) == 0))
then 
    # check to see if the correct deb file has already been downloaded
    if [ -f $DATA/$OPENCV4TEGRA_VERSION".deb" ]
    then
        CSUM="830eb30bbf6d5dbaa43358f14fc6139b"
        MD5=$(md5sum $DATA/$OPENCV4TEGRA_VERSION".deb" | cut -d ' ' -f 1)
        if [ "$MD5" != "$CSUM" ]
        then
            sudo rm -f $OPENCV4TEGRA_VERSION".deb"
            printf ${YELLOW}"Found invalid file "$OPENCV4TEGRA_VERSION".deb. Program will attempt to download it again.\N"${NO_COLOR}
            wget http://developer.download.nvidia.com/embedded/OpenCV/L4T_21.2/$OPENCV4TEGRA_VERSION".deb" -P $DATA
        else
            printf ${GREEN}"Found valid file "$OPENCV4TEGRA_VERSION".deb\n"${NO_COLOR}
        fi
    else
        wget http://developer.download.nvidia.com/embedded/OpenCV/L4T_21.2/$OPENCV4TEGRA_VERSION".deb" -P $DATA
    fi

    # do the official OpenCV4Tegra installation since it's not yet installed
    sudo dpkg -i $DATA/$OPENCV4TEGRA_VERSION".deb"
    sudo apt-get update
    sudo apt-get install $APTITUDE_OPTIONS libopencv4tegra libopencv4tegra-dev libopencv4tegra-python

else 
    # do this to be safe
    sudo apt-mark unhold libopencv4tegra libopencv4tegra-dev libopencv4tegra-python libopencv4tegra-repo
fi 

# either way, then UNINSTALL OpenCV4Tegra to fix the debian files
sudo apt-get remove $APTITUDE_OPTIONS libopencv4tegra libopencv4tegra-dev libopencv4tegra-python 
sudo dpkg --purge libopencv4tegra libopencv4tegra-dev libopencv4tegra-python

# download modded debian files from GitHub 
# this may look very suspicious but it's nothing more than pacakged deb files made as per instructions 
# from https://devtalk.nvidia.com/default/topic/835118/embedded-systems/incorrect-configuration-in-opencv4tegra-debian-packages-and-solution
# TODO: script this step
wget https://raw.githubusercontent.com/Pleiades-Spiri/opencv4tegra-mod-deb-files/master/libopencv4tegra_2.4.10.1_armhf_mod.deb -P $DATA
wget https://raw.githubusercontent.com/Pleiades-Spiri/opencv4tegra-mod-deb-files/master/libopencv4tegra-dev_2.4.10.1_armhf_mod.deb -P $DATA
wget https://raw.githubusercontent.com/Pleiades-Spiri/opencv4tegra-mod-deb-files/master/libopencv4tegra-python_2.4.10.1_armhf_mod.deb -P $DATA

# source the prepared debian files to reinstall OpenCV4Tegra properly
sudo dpkg -i $DATA/libopencv4tegra_2.4.10.1_armhf_mod.deb 
sudo dpkg -i $DATA/libopencv4tegra-dev_2.4.10.1_armhf_mod.deb 
sudo dpkg -i $DATA/libopencv4tegra-python_2.4.10.1_armhf_mod.deb

# install OpenCV4Tegra again
echo "Installing OpenCV4Tegra"
sudo apt-get update
sudo apt-get install $APTITUDE_OPTIONS libopencv4tegra libopencv4tegra-dev libopencv4tegra-python

echo "To avoid future conflicts, tell the system to retain the present installation of the OpenCV4Tegra library"
sudo apt-mark hold libopencv4tegra libopencv4tegra-dev libopencv4tegra-python libopencv4tegra-repo
printf ${GREEN}"OpenCV4Tegra installed and debian files fixed \n"${NO_COLOR}

###############################################################################
# Install ROS 
###############################################################################
# taken from https://pixhawk.org/dev/ros/installation

# add the ROS repository for Trusty(14.04):
sudo sh -c 'echo "deb http://packages.ros.org/ros/ubuntu trusty main" > /etc/apt/sources.list.d/ros-latest.list'

# get the official ROS key
wget https://raw.githubusercontent.com/ros/rosdistro/master/ros.key -O - | sudo apt-key add -

# and install the packages we recommend for having a development environment good for MAVs. 
# you can install any other project-specific dependencies via rosdep or manually.
sudo apt-get update
sudo apt-get install $APTITUDE_OPTIONS \
                    ros-indigo-ros-base \
                    ros-indigo-mavlink \
                    ros-indigo-mavros \
                    ros-indigo-tf

# setup rosdep which makes life a lot easier by auto-installing dependencies wherever possible.
sudo apt-get install $APTITUDE_OPTIONS python-rosdep
sudo rosdep init
rosdep update

# gotta source the ROS binary path
echo "source /opt/ros/indigo/setup.bash" >> $SPIRI_HOME/.bashrc
source /opt/ros/indigo/setup.bash

printf ${GREEN}"ROS and basic packages successfully installed \n"${NO_COLOR}

###############################################################################
# Install SpiriGo repository
###############################################################################
SPIRI_WORKSPACE=$SPIRI_HOME/spiri_ws

# make workspace directory then "compile" to initialize the ROS workspace
mkdir -p $SPIRI_WORKSPACE/src
cd $SPIRI_WORKSPACE/src
catkin_init_workspace
cd $SPIRI_WORKSPACE
catkin_make 

# download spiri_go package into source 
cd $SPIRI_WORKSPACE/src
git clone https://github.com/Pleiades-Spiri/spiri_go.git

# now compile the one package we have
cd $SPIRI_WORKSPACE
catkin_make

# gotta source the package paths for roslaunch/rosrun to find them
sh -c "echo 'source $SPIRI_WORKSPACE/devel/setup.bash' >> $SPIRI_HOME/.bashrc"
source $SPIRI_WORKSPACE/devel/setup.bash

printf ${GREEN}"SpiriGo package installed and compiled successfully \n"${NO_COLOR}

###############################################################################
# All done!
###############################################################################
cd $SPIRI_HOME

cat <<CONCLUSION

Congratulations! SpiriGo is now installed on your Jetson TK1.

Please refer to the readme online for more instructions:
    
    https://github.com/Pleiades-Spiri/spiri_go

If everything compiled fine, you should be able to run:

    roslaunch spiri_go jetson.launch

And connect to your Pixhawk as long as your serial connection is set up 
properly.
CONCLUSION
