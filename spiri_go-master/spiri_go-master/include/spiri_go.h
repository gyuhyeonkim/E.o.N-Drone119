#pragma once

#ifndef __SPIRIGO_H
#define __SPIRIGO_H
 

#include "ros/ros.h"
#include "geometry_msgs/Twist.h"
#include "geometry_msgs/Vector3Stamped.h"
#include "geometry_msgs/Pose.h"
#include "sensor_msgs/Image.h"
#include "sensor_msgs/Imu.h"
#include "tf/tfMessage.h"
#include "tf/transform_listener.h"
#include "tf/transform_broadcaster.h"
#include "tf2_ros/buffer.h"
#include "tf2_ros/transform_listener.h"
#include <tf2_ros/transform_broadcaster.h>
#include <tf2/LinearMath/Matrix3x3.h>
#include "std_msgs/Empty.h"
#include "std_srvs/Empty.h"
#include "mavros_msgs/State.h"
#include <spiri_data_classes.h>
#include <actionlib/server/simple_action_server.h>
#include <spiri_go/TakeoffAction.h>
#include <spiri_go/LocalPosition.h>
#include <spiri_go/LandHereAction.h>
#include <spiri_go/LastState.h>
#include <string>

using namespace std;

class SpiriGo
{
private:
    // subscribers
    // the local position of the copter
    ros::Subscriber local;
    // the state of the copter
    ros::Subscriber state;

    // publishers
    // sets the velocity
    ros::Publisher vel;
    ros::Publisher rc_out;

    // service clients
    // Arm the copter
    ros::ServiceClient arm;
    // a service to set the mode
    ros::ServiceClient set_mode;
    // tell the copter to take off
    ros::ServiceClient takeoff;
    // MAVLink service for other needs
    ros::ServiceClient mavlink_cmd_srv;

    ros::NodeHandle nh;

    // save last imu received for forwarding...
    sensor_msgs::Imu lastLocalReceived;

    // ROS message callbacks
    void localSubCb(const geometry_msgs::PoseStamped localPtr);
    void stateSubCb(const mavros_msgs::State);

    // state variables
    mavros_msgs::State last_state;
    bool taking_off; // has taken off
    bool flying; // has finished taking off

    // location last returned by mavros
    geometry_msgs::Pose last_location;
    // guess of nearest location, itegrated using velocity
    geometry_msgs::Pose location;
    // target velocity
    geometry_msgs::Twist velocity;

    // internal control methods
    geometry_msgs::Quaternion getOrientation();
    bool setMode(const char* targetMode);
    bool setArmed();
    bool setGuided();
    void takeOff(float targetAlt);

    // service callbacks
    bool getLocalPositionSCB(spiri_go::LocalPosition::Request &req, spiri_go::LocalPosition::Response &rsp);
    bool getLastStateSCB(spiri_go::LastState::Request &req, spiri_go::LastState::Response &rsp);

    void updateLocation(ros::Duration dt);

public:

    // action servers
    actionlib::SimpleActionServer<spiri_go::TakeoffAction> takeoff_as;
    actionlib::SimpleActionServer<spiri_go::LandHereAction> land_here_as;

    // services, should all be a descriptive name with "Service" at the end
    // Their corresponding call back should have the same name, replacing "Service" with SCB
    // thus if you have a service named doSomethingService, it's call back should be doSomethingSCB
    ros::ServiceServer getLocalPositionService;
    ros::ServiceServer getLastStateService;

    SpiriGo();
    ~SpiriGo();

    // getter functions 
    bool isArmed();
    bool isControllable();
    std::string getMode();
    geometry_msgs::Point getLocalPosition();
    SpiriAttitude getAttitude();

    // Yaw commander with MAVLink; angles are in degrees 
    // Eigen::Vector3d getAttitude();


    // basic Spiri control functions
    void armAndTakeOff(const spiri_go::TakeoffGoalConstPtr& goal);
    void landHere(const spiri_go::LandHereGoalConstPtr& goal);
    void conditionYaw(float targetYaw, float targetYawRate);
    void setENUVelocity(/*double eastwardVelocity, double northwardVelocity*/);
    void setHorizontalVelocity(double u, double v);

    // main pose-estimation loop
    void Loop();

};
#endif /* __SPIRIGO_H */
