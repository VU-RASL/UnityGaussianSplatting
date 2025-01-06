
using System.IO;
using System.Collections.Generic;
using UnityEngine;
using Newtonsoft.Json;
using UnityEngine.UIElements;
using System.Reflection.Emit;
using Unity.Mathematics;
using System;

public class SavePose : MonoBehaviour
{
    public SMPLX smplx;

    public bool save = false;

    public SMPLX.BodyPose pose;
    SMPLX.BodyPose prev_pose;

    SMPLXPoseData data;

    string[] _bodyJointNames = new string[] {"left_hip","right_hip","spine1","left_knee","right_knee","spine2","left_ankle","right_ankle","spine3", "left_foot","right_foot","neck","left_collar","right_collar","head","left_shoulder","right_shoulder","left_elbow", "right_elbow","left_wrist","right_wrist"};
    string[] _handLeftJointNames = new string[] { "left_index1","left_index2","left_index3","left_middle1","left_middle2","left_middle3","left_pinky1","left_pinky2","left_pinky3","left_ring1","left_ring2","left_ring3","left_thumb1","left_thumb2","left_thumb3" } ;
    string[] _handRightJointNames = new string[] { "right_index1","right_index2","right_index3","right_middle1","right_middle2","right_middle3","right_pinky1","right_pinky2","right_pinky3","right_ring1","right_ring2","right_ring3","right_thumb1","right_thumb2","right_thumb3" } ;

    Dictionary<string, Transform> _transformFromName;

    public void Start()
    {
        prev_pose = pose;
        smplx.SetBodyPose(pose);
        if (_transformFromName == null)
        {
            _transformFromName = new Dictionary<string, Transform>();
            Transform[] transforms = smplx.gameObject.transform.GetComponentsInChildren<Transform>(true);
            foreach (Transform t in transforms)
            {
                _transformFromName.Add(t.name, t);
            }
        }
    }

    public void Update()
    {
        if(pose!=prev_pose)
        {
            prev_pose = pose;
            smplx.SetBodyPose(pose);
        }
        if(save)
        {
            save = false;
            UpdateData();
            SavePoseToFile();
        }
    }

    // private void SavePoses(){
    //     smplx.SetBodyPose(SMPLX.BodyPose.T);
    //     UpdateData();
    //     SavePoseToFile("Assets/HahaData/BodyPoses/T.json");
    //     smplx.SetBodyPose(SMPLX.BodyPose.A);
    //     UpdateData();
    //     SavePoseToFile("Assets/HahaData/BodyPoses/A.json");
    //     smplx.SetBodyPose(SMPLX.BodyPose.C);
    //     UpdateData();
    //     SavePoseToFile("Assets/HahaData/BodyPoses/C.json");
    // }

    private void UpdateData()
    {
        data = new SMPLXPoseData{
            transl = new float[3],
            global_orient = new float[3],
            body_pose = new float[21*3],
            left_hand_pose = new float[15*3],
            right_hand_pose = new float[15*3],
            leye_pose = new float[3],
            reye_pose = new float[3],
            jaw_pose = new float[3],
            expression = new float[10],
            betas = new float[10]
        };

        GetRodriguezNotation(_transformFromName["pelvis"], out Vector3 orient);
        GetRodriguezNotation(_transformFromName["left_eye_smplhf"], out Vector3 leye);
        GetRodriguezNotation(_transformFromName["right_eye_smplhf"], out Vector3 reye);
        GetRodriguezNotation(_transformFromName["jaw"], out Vector3 jaw);
        for(int i = 0; i < 3; i++)
        {
            data.transl[i] = _transformFromName["root"].position[i];
            data.global_orient[i] = orient[i];
            data.leye_pose[i] = leye[i];
            data.reye_pose[i] = reye[i];
            data.jaw_pose[i] = jaw[i];
        }
        for(int i = 0; i< 10; i++)
        {
            data.expression[i] = smplx.expressions[i];
            data.betas[i] = smplx.betas[i];
        }
        for(int i = 0; i < _bodyJointNames.Length; i++)
        {
            Debug.Log(_bodyJointNames[i]);
            GetRodriguezNotation(_transformFromName[_bodyJointNames[i]], out Vector3 output);
            Debug.Log(output);
            data.body_pose[3*i] = output.x;
            data.body_pose[3*i+1] = output.y;
            data.body_pose[3*i+2] = output.z;
        }
        for(int i = 0; i < _handLeftJointNames.Length; i++)
        {
            GetRodriguezNotation(_transformFromName[_handLeftJointNames[i]], out Vector3 left);
            data.left_hand_pose[3*i] = left.x;
            data.left_hand_pose[3*i+1] = left.y; 
            data.left_hand_pose[3*i+2] = left.z;
            GetRodriguezNotation(_transformFromName[_handRightJointNames[i]], out Vector3 right);
            data.right_hand_pose[3*i] = right.x;
            data.right_hand_pose[3*i+1] = right.y;
            data.right_hand_pose[3*i+2] = right.z;
        }
        
    }

    void SavePoseToFile()
    {
        string path = "Assets/HahaData/BodyPoses/";
        string fullpath = path;
        switch(pose)
        {
            case SMPLX.BodyPose.T:
                fullpath += "T.json";
                break;
            case SMPLX.BodyPose.A:
                fullpath += "A.json";
                break;
            case SMPLX.BodyPose.C:
                fullpath += "C.json";
                break;                    
        }
        string json = JsonConvert.SerializeObject(data);

        // Write the JSON to a file
        File.WriteAllText(fullpath, json);
    }

    void GetRodriguezNotation(Transform t, out Vector3 axangle )
    {
        Quaternion q = t.rotation;
        // q = new Quaternion(-q.x, q.y, q.z, -q.w);
        
        q.ToAngleAxis(out float angle, out Vector3 axis);
        // angle *= -1;
        // axis.x *= -1;
        angle *= 3.1415962f/180.0f;
        axangle = angle*axis.normalized;
        axangle.x = Math.Abs(axangle.x) < 1e-4 ? 0f : axangle.x;
        axangle.y = Math.Abs(axangle.y) < 1e-4 ? 0f : axangle.y;
        axangle.z = Math.Abs(axangle.z) < 1e-4 ? 0f : axangle.z;
        // axangle = QuaternionToRodrigues(t.rotation);
    }

    Vector3 QuaternionToRodrigues(Quaternion unityQuat)
    {
        // Step 1: Convert from Unity's left-handed to SMPL-X's right-handed system
        Quaternion smplXQuat = new Quaternion(-unityQuat.x, unityQuat.y, unityQuat.z, -unityQuat.w);

        // Step 2: Convert quaternion to Rodrigues vector
        Vector3 vectorPart = new Vector3(smplXQuat.x, smplXQuat.y, smplXQuat.z);
        float scalarPart = smplXQuat.w;

        // Rodrigues formula: r = 2 * (v / w)
        if (Mathf.Abs(scalarPart) > Mathf.Epsilon)
        {
            return 2.0f * (vectorPart / scalarPart);
        }
        else
        {
            // Handle the degenerate case where w is close to zero
            return Vector3.zero; // Identity rotation or undefined
        }
    }

    private class SMPLXPoseData
    {
        public float[] transl;
        public float[] global_orient;
        public float[] body_pose;
        public float[] left_hand_pose;
        public float[] right_hand_pose;
        public float[] leye_pose;
        public float[] reye_pose;
        public float[] jaw_pose;
        public float[] expression;
        public float[] betas;

    }
}
