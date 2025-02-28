using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using Newtonsoft.Json;
using UnityEngine;
using Unity.Mathematics;
// using System.Numerics;

[ExecuteInEditMode]
public class smplxImporter : MonoBehaviour
{
    public string folderPath = "Assets/smplx_example/";  // Path to folder containing JSON files
    public List<HahaAvatarDataS> allData = new List<HahaAvatarDataS>(); // Store all loaded data
    [SerializeField] public SMPLX smplx; // Reference to the SMPL-X model
    [SerializeField] public Transform root;
    // SMPL-X joint names in Unity
    public string[] _bodyJointNames = new string[] { 
        "pelvis",
        "left_hip",
        "right_hip",
        "spine1",
        "left_knee",
        "right_knee",
        "spine2",
        "left_ankle",
        "right_ankle",
        "spine3",
        "left_foot",
        "right_foot",
        "neck",
        "left_collar",
        "right_collar",
        "head",
        "left_shoulder",
        "right_shoulder",
        "left_elbow", 
        "right_elbow",
        "left_wrist",
        "right_wrist"
    };

    void Start()
    {
        ReadAllJsonFiles();

        // Start coroutine to apply poses with delay INSIDE the loop
        StartCoroutine(ApplyPosesWithDelay(0.2f)); // Adjust delay (in seconds)
    }

    IEnumerator ApplyPosesWithDelay(float delay)
    {
        foreach (HahaAvatarDataS avatarData in allData)
        {
            // root.transform.rotation = avatarData.root_pose_quaternions[0]* Quaternion.Euler(0, 180, 180);            
            
            
            if (avatarData.body_pose_quaternions != null)
            {
                Debug.Log($"Applying 'body_pose' with {avatarData.body_pose_quaternions.Count} joints:");

                for (int i = 0; i < avatarData.body_pose_quaternions.Count; i++)
                {

                    Quaternion jointRotation = avatarData.body_pose_quaternions[i] ;
        
                    string jointName = _bodyJointNames[i];

                    // jointRotation = avatarData.body_pose_quaternions[i];

                    // if (jointName == "pelvis" )
                    // {
                    //     jointRotation = avatarData.body_pose_quaternions[i] * Quaternion.Euler(180,0,0);
                    // }
                    smplx.SetLocalJointRotation(jointName, jointRotation);
                    
                }


                smplx.UpdatePoseCorrectives();
                // smplx.UpdateJointPositions(true);

                yield return new WaitForSeconds(delay);
            }
            else
            {
                Debug.LogError("body_pose_quaternions is null for one of the files.");
            }
        }
        smplx.ResetBodyPose();
        
    }

    void ReadAllJsonFiles()
    {
        allData.Clear(); 
        string fullPath = Path.Combine(Application.dataPath, "smplx_example");

        if (!Directory.Exists(fullPath))
        {
            Debug.LogError($"Folder not found: {fullPath}");
            return;
        }

        // Get all JSON files
        string[] jsonFiles = Directory.GetFiles(fullPath, "*.json");

        if (jsonFiles.Length == 0)
        {
            Debug.LogError($"No JSON files found in: {fullPath}");
            return;
        }

        // Sort files by extracting numeric values from filenames
        Array.Sort(jsonFiles, (a, b) =>
        {
            int numA = ExtractNumber(Path.GetFileNameWithoutExtension(a));
            int numB = ExtractNumber(Path.GetFileNameWithoutExtension(b));
            return numA.CompareTo(numB);
        });

        // Read JSON files in correct order
        foreach (string filePath in jsonFiles)
        {
            Debug.Log(filePath);
            HahaAvatarDataS data = new HahaAvatarDataS(filePath);
            if (data.body_pose_quaternions != null) 
            {
                allData.Add(data);
            }
        }

        Debug.Log($"Loaded {allData.Count} JSON files successfully.");
    }

    // Helper method to extract numbers from filenames
    private int ExtractNumber(string filename)
    {
        if (int.TryParse(filename, out int number))
        {
            return number;
        }
        return int.MaxValue; // Ensure non-numeric filenames (if any) go to the end
    }

    void OnDisable()
    {
        allData.Clear();
        Debug.Log("Cleared all loaded JSON data.");
    }

    void OnDestroy()
    {
        allData.Clear();
        
    }
}

[Serializable]
public class HahaAvatarDataS
{
    public List<Quaternion> body_pose_quaternions; 
    // public List<Quaternion> root_pose_quaternions; 

    public class HahaOutputDataS
    {
        public float[][] body_pose;
        public float[] root_pose;  // Single row (1x3) per file
    }

    public HahaAvatarDataS(string path)
    {
        body_pose_quaternions = new List<Quaternion>();
        if (!File.Exists(path))
        {
            Debug.LogError($"JSON file not found at: {path}");
            return;
        }

        string content = File.ReadAllText(path);
        HahaOutputDataS data = JsonConvert.DeserializeObject<HahaOutputDataS>(content);

        if (data == null || data.body_pose == null || data.root_pose == null)
        {
            Debug.LogError($"Error: Missing 'body_pose' or 'root_pose' in {path}");
            return;
        }

        // Print shape of body_pose
        int numRows = data.body_pose.Length;
        int numCols = numRows > 0 ? data.body_pose[0].Length : 0;
        Debug.Log($"Shape of 'body_pose' in {Path.GetFileName(path)}: {numRows} x {numCols}");

        // Convert Rodrigues rotation vectors to Quaternions
        body_pose_quaternions.Add(ConvertSingleRodriguesToQuaternion(data.root_pose));
        // body_pose_quaternions[0] = reorient(body_pose_quaternions[0]);
        body_pose_quaternions.AddRange(ConvertToQuaternions(data.body_pose));
        
         
        // root_pose_quaternions = new Quaternion[1] { ConvertSingleRodriguesToQuaternion(data.root_pose) };  // Convert 1x3 root_pose
    }


    private List<Quaternion> ConvertToQuaternions(float[][] inputNestedMatrix)
    {
        int rowCount = inputNestedMatrix.Length;
        List<Quaternion> outputList = new List<Quaternion>();

        for (int rowIndex = 0; rowIndex < rowCount; rowIndex++)
        {

            outputList.Add(QuatFromRodrigues(inputNestedMatrix[rowIndex][0],
                                                        inputNestedMatrix[rowIndex][1],
                                                        inputNestedMatrix[rowIndex][2]));
        }
        return outputList;
    }
    private Quaternion ConvertSingleRodriguesToQuaternion(float[] inputVector)
    {
        if (inputVector == null || inputVector.Length != 3)
        {
            Debug.LogError("Error: root_pose is missing or not in [x,y,z] format.");
            return Quaternion.identity;
        }

        return QuatFromRodrigues(inputVector[0], inputVector[1], inputVector[2]);
    }

public static Quaternion QuatFromRodrigues(float rodX, float rodY, float rodZ)
{
        Vector3 axis = new Vector3(-rodX, rodY, rodZ);
        float angle_deg = - axis.magnitude * Mathf.Rad2Deg;
        Vector3.Normalize(axis);

        Quaternion quat = Quaternion.AngleAxis(angle_deg, axis)  ;
        return quat;
}

private Quaternion diff(Quaternion a, Quaternion b){
    // return quat diff such that a*diff = b
    Quaternion diff = Quaternion.Inverse(a) * b;
    return diff;
}

private Quaternion reorient(Quaternion a){
    Quaternion forward = Quaternion.LookRotation(Vector3.forward, Vector3.up);

    a *= diff(a, forward);
    return a;
}

}





        // smplx.ResetBodyPose();
        // smplx.SetBetaShapes();
        
        
        // smplx.SetLocalJointRotation("left_collar", Quaternion.Euler(0.0f, 0.0f, -60.0f));
        // smplx.SetLocalJointRotation("left_shoulder", Quaternion.Euler(0.0f, 0.0f, 35.0f));
        // smplx.SetLocalJointRotation("right_collar", Quaternion.Euler(0.0f, 0.0f, 60.0f));
        // smplx.SetLocalJointRotation("right_shoulder", Quaternion.Euler(0.0f, 0.0f, -35.0f));
        // smplx.SetLocalJointRotation("right_hip", Quaternion.Euler(0.0f, 30.0f, 0.0f));
        // smplx.SetLocalJointRotation("left_hip", Quaternion.Euler(0.0f, -30.0f, 0.0f));
        // smplx.SetLocalJointRotation("left_knee", Quaternion.Euler(30.0f, 30.0f, 0.0f));
        // smplx.UpdatePoseCorrectives();
        // smplx.UpdateJointPositions(true);
