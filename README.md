# EyeAndNeckTracking
### About the project
There are 2 scenes in the project

* DataCollection
* ModelTest
  
ModelTest is to simulate the learning model.And the DataCollection is to collect the human data

#### About DataCollection
The are 4 tasks in the scenes. Each task has 3 trials.
* Smooth Linear Pursuit
* Smooth Arc pursuit
* Rapid Movement
* Rapid Avoidance Movement

The scenes start from `Trial` object, which can be found in `hierarchy`. There is `DataCollection2`, the script, in `Trial`. All the procees of Data Collection is written in here. The data collection functions are also written in `DataCollection2`

![image](https://github.com/aria-lab-code/EyeTracking_VR_DataCollection/assets/113972450/8de429de-db4f-4f2b-b4b7-95a9711f9f5b)

The script and the relationship between the object is below.

** object -> script **
* Tracking Object1 -> Smooth Pursuit Linear
* Tracking Object2 -> Smooth Pursuit Arc
* Gaze Focusable Object 1 -> Highlight At Gaze
* Gaze Focusable Object 2 -> Highlight At Gaze
* Gaze Focusable Object 3 -> Highlight At Gaze
* Gaze Focusable Object 4 -> Highlight At Gaze
* Gaze Focusable Object 5 -> Highlight At Gaze
* Gaze Focusable Object 6 -> Highlight At Gaze
* Gaze Avoid Object 1 -> Avoid Obstacle Test
* Gaze Avoid Object 2 -> Avoid Obstacle Test
* Gaze Avoid Object 3 -> Avoid Obstacle Test

#### About ModelTest
The are 3 tasks in the scenes. 
* Smooth Linear Pursuit
* Smooth Arc pursuit
* Rapid Movement

The scenes start from `Simulation` object,There is `ModelSim` in `Simulation`.

![image](https://github.com/aria-lab-code/EyeTracking_VR_DataCollection/assets/113972450/8de429de-db4f-4f2b-b4b7-95a9711f9f5b)

The script and the relationship between the object is below.

** object -> script **
* Tracking Object1 -> Smooth Pursuit Linear
* Tracking Object2 -> Smooth Pursuit Arc
* Gaze Focusable Object 1 -> Highlight At Gaze
* Gaze Focusable Object 2 -> Highlight At Gaze
* Gaze Focusable Object 3 -> Highlight At Gaze
