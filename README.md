# Tether

**Important:**  
This Unity project is monolithic and contains a lot of unrelated game-logic code and networking libraries (Fishnet, MasterServerToolKit) used to build a DVE as a game. As a result, cloning, opening, and building can be time-consuming. Only the **HSPDIM** and **CoreInGame** scenes are needed to reproduce our interest-matching experiments; you can ignore the rest.

## Prerequisites
- **Unity Hub** and the required 6000.0.16f1 version.  
- An internet connection for initial package import.

## Opening the Project
*(This step may take up to 1 hour on first load)*
1. Clone the repository:  `https://github.com/LeThaiThinh18021228/Tether.git`  
2. Open Unity Hub → **Add** → select the cloned folder.  
3. When prompted, install or switch to the required Unity Editor version and wait for asset import.

## Quick Benchmark Validation

1. In Unity, open the **HSPDIM** scene (`Assets/Scenes/HSPDIM.unity`).  
2. Press **Play** in the Editor.  
3. View the benchmark output in the **Console** under Warning messages (see **Observing Results**).  
4. To adjust benchmark parameters:  
   1. Select the **HSPDIMTest** GameObject in the Hierarchy.  
   2. In the Inspector, configure:  
      - **Alpha**: the coverage ratio (0.0–1.0) used when “Use Alpha” is enabled.  
      - **Size**: the fixed range size used when “Use Alpha” is disabled.  
      - **Use Alpha** (bool):  
        - **On** → ranges are sized by the **Alpha** ratio.  
        - **Off** → ranges use the **Size** value instead.  
      - **Count Range**: total number of subscription and update ranges (`m + n`).  
      - **Thread Count**: number of parallel worker threads for matching.  
      - **Modify Ratio**: fraction of ranges modified each cycle (e.g. 0.1 = 10%).  

## Visual Validation
1. Open the **CoreInGame** scene (`Assets/Scenes/CoreInGame.unity`).  
2. In the menu, go to **Tools → Custom Build Window -> Click on BuildWinMaster**.  
3. In the Inspector, ensure **Enable** is checked.  
4. Click **Build All**. When it finishes, run `MasterServer.exe` to start the server.  
5. Back in the Unity Editor, press **Play**, then click the **Server** and **Client** buttons in the Game view.  
6. To adjust parameters:  
   - **Currency Generator** (in Hierarchy): set **Max Currency** to change the update range.  
   - **Bot Manager** (in Hierarchy): set **Init Bot** to change both subscription and update counts.

## Observing Results
Check the **Console** (Warnings) for output lines like these:

![image](https://github.com/user-attachments/assets/f03757ce-df55-4ad1-9031-68ea5dc18bad)

- Thread Count: 8, SubModCount: 15, UpModCount: 15 over 65
- exeTotalTime : 1.3118 # total runtime
- exeTotalTimeMapping : 0.2446 # mapping step
- exeTotalTimeRecalculate : 0.0133 # recalculation step
- exeTotalTimeInput : 0.2835 # preprocessing
- exeTotalTimeMatching : 0.0831 # matching step
- exeTimeOutput : 0 # post-processing
