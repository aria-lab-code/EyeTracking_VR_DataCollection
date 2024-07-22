# A dataset of paired head and eye movements during visual tasks in virtual environments
Repository Contributors: _Colin Robow, Chia-Hsuan Tsai, Jordan Thompson, Eric Brewer, Connor Mattson, Daniel S. Brown, Haohan Zhang_

### About the project
Humans use coordinated head and eye movement to effectively survey, gain information, and interact with their environment. Our work introduces a open-access dataset consisting of N=20 users Neck and Eye oritentation during 4 simulated tasks. This repository contains the required Unity/CSharp scripts required to reproduce the human trials of our paper (link available soon).

The original dataset is available for use at [this link](https://figshare.com/articles/dataset/EyeTrackingVRDataset/25749378).

_Funding disclosure: This work is supported by the National Institute of Biomedical Imaging and Bioengineering (R21-EB035378)._

### Requirements
Software
* Python >= 3.9.13
* Unity 2021.3.8
* SteamVR

Hardware
* VIVE Pro Eye, HTC Corporation

### Experiment Reproducability
We engineered 4 tasks for users to participate in (shown below). Users conducted each task 3 times.
* Smooth Linear Pursuit: Follow a target that moves linearly between random positions.
* Smooth Arc Pursuit: Follow a target moving on circular trajectories. 
* Rapid Movement: Eliminate targets moving toward the user before they collide with the camera.
* Rapid Avoidance Movement: Eliminate blue targets moving toward the user while avoiding gazing at the yellow objects.

The experiments are all conducted sequentially using the Unity Scene titled [DataCollection.unity](EyeTrackingTest/Assets/Scenes/DataCollection.unity). To set up the experiment, use the following steps:
1. At the root of the project, create a file "userIDList.txt". This allows you to explicitly enumerate identifiers for users (one on each line). The script requires at least one line in this file, so please add YOUR_NAME to the file. NOTE: At the end of each user experiment, the top most element from this file gets deleted. You will need to make sure there is a user each time you run an experiment.
2. Ensure that your hardware is recognized and connected to your computer and equip at least one hand controller.
3. Sit upright with the headset on and look forward as you recenter the display using the on-board centering function. Remove the headset.
4. Open [DataCollection.unity](EyeTrackingTest/Assets/Scenes/DataCollection.unity) in Unity.
5. At the top of the window push the Play button.
6. Place the Head Mounted Display on your head. You will see a calibration sequence to set up the eye tracker.
7. Following calibration, you should be in a virtual environment with instructions appearing in front of you. NOTE: if you do NOT see instructions, make sure that you have completed step 1 correctly. The instructions will not appear if a user has not been explicitly defined in "userIDList.txt".
8. Follow the instructions in the software and participate in training and all 12 trials.
9. After the conclusion of the experiment, terminate the program by clicking the play button again.
10. Examine your data at the project's root directory with the names "Object{USERID}-\*.txt" (The object locations for each trial) and "User{USERID}-\*.txt"

If you are unable to reproduce these steps, please submit an GitHub issue in this repository.

### Building Your Own Tasks/Experiments
We encourage the use of our project as a basis for continued work collecting neck and eye data. While we cannot address all the possible extensions that you may want to implement, we highlight some of the specific code implementations here that will help to explain how the experiments work. These files can serve as examples to follow as you develop your own experiments.

| Script    | Description |
| -------- | ------- |
| [GazeCollection2.cs](EyeTrackingTest/Assets/Scripts/GazeCollection2.cs)  | The main script for data collection. Uses a state machine to determine which task is currently running and intializes the corresponding objects. Keeps track of time and score. Calibration and UI Text declared here. |
| [SmoothPursuitLinear.cs](EyeTrackingTest/Assets/Scripts/SmoothPursuitLinear.cs) | The script attached to unity cubes that moves the cubes linearly in space (Linear Pursuit Task). Highlights Cubes green when the gaze vector intersects the cube.  |
| [SmoothPursuitArc.cs](EyeTrackingTest/Assets/Scripts/SmoothPursuitArc.cs) | The script attached to unity cubes that moves the cubes along a circular trajctory in space (Arc Pursuit Task). Highlights Cubes green when the gaze vector intersects the cube.  |
| [HighlightAtGaze.cs](EyeTrackingTest/Assets/Scripts/HighlightAtGaze.cs) | The script attached to unity cubes that initializes and moves the cubes toward the camera (Rapid Movement and Rapid Avoidance Movement Tasks). Highlights Cubes green when the gaze vector intersects the cube and destroys the cubes when you look at them for enough time.  |
| [AvoidObstacleTest.cs](EyeTrackingTest/Assets/Scripts/AvoidObstacleTest.cs) | The script attached to unity cubes that initializes and moves the cubes toward the camera (Rapid Avoidance Movement Task). Highlights Cubes red when the gaze vector intersects the cube.  |

### Ongoing Development
The scene [ModelTest.unity](EyeTrackingTest/Assets/Scenes/ModelTest.unity) is to simulate a learned model using LSTM, GRU, and MLP implementations found in the [DataProcessing](DataProcessing) directory. This work is ongoing and much of the code may be overwritten or deprecated in the future.

### Contact Us
If you run into problems with this repository, please submit an GitHub issue.

#### Unity Object/Script Details

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
