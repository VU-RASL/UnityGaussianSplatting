using System.Collections;
using System.Collections.Generic;
using UnityEngine;
using Unity.Mathematics;
using System;

public class VisualDataChecker : MonoBehaviour
{
    public string path = "Assets/HahaData/state_dict.json";

    public bool visualize = false;
    public bool toggleTexture = false;
    public bool clearAll = false;

    private HahaAvatarData data;

    private SkinnedMeshRenderer smr;

    public Vector3[] V;
    public float3[] T;
    public float4[] R;
    public float[] k;

    private Material originalMat;

    void Clear()
    {
        smr.sharedMaterial = originalMat;
        DestroyGaussians();
    }

    void Update()
    {
        if(clearAll)
        {
            clearAll = false;
            Clear();
        }

        if(toggleTexture)
        {
            toggleTexture = false;
            smr.enabled = !smr.enabled;
        }

        if(visualize)
        {
            visualize = false;
            smr = GetComponent<SkinnedMeshRenderer>();
            if (smr == null)
            {
                Debug.LogError("SkinnedMeshRenderer not attached to game object!");
                return;
            }
            data = new HahaAvatarData(path);
            originalMat = smr.sharedMaterial;
            ApplyTex();
            InitVTRK();
            CreateGaussians();
        }
    }

    void ApplyTex()
    {
        
        var mat = new Material(smr.sharedMaterial);
        mat.SetTexture("_MainTex", data.texture);
        smr.sharedMaterial = mat;
    }

    void InitVTRK()
    {
        V = smr.sharedMesh.vertices;

        T = new float3[data.faceCount];
        Array.Fill(T,float.NaN);
        R = new float4[data.faceCount];
        Array.Fill(R,float.NaN);
        k = new float[data.faceCount];
        Array.Fill(k,float.NaN);

        for(int i = 0; i < data.splatCount; i++)
        {
            int idf = data.gaussianToFace[i];
            
            if(math.isnan(T[idf].x))
            {
                CalcFaceTransform(idf);
            }
        }


    }

    void CalcFaceTransform(int idf)
    {
        int3 verts = data.facesToVerts[idf];
        float3 v0 = V[verts.x];
        float3 v1 = V[verts.y];
        float3 v2 = V[verts.z];

        // Calculate translation (T): Mean of vertices
        T[idf] = (v0 + v1 + v2) / 3.0f;

        // Calculate vectors for face edges
        // float3 vec1 = normalize(v1 - v0);
        // float3 vec2 = normalize(v2 - v0);
        float3 vec1 = v2 - v1;
        float3 vec2 = v0 - v1;
        float3 vec3 = v0 - v2;

        // Calculate normal and orthonormal basis
        float3 v12cross = math.cross(vec1,vec2);
        float3 norm = math.normalize(v12cross);
        vec1 = math.normalize(vec1);
        float3 prod = math.normalize(math.cross(vec1, norm));

        // Build rotation matrix
        float3x3 rotmat = new float3x3(vec1, norm, prod);
        rotmat = math.transpose(rotmat);

        // Convert rotation matrix to quaternion (R)
        R[idf] = MatrixToQuaternion(rotmat);
        
        // Calculate area and edge lengths for scaling factor (k)
        float area = math.length(v12cross);
        float vec3_length = math.length(vec3);
        float h = area / vec3_length;

        k[idf] =  (h + vec3_length) / (2.0f * 0.05f); // MAX_SCALE = 0.05
    }

    void CreateGaussians()
    {
        for(int i = 0; i < data.splatCount; i++)
        {
            int idf = data.gaussianToFace[i];

            float3 gOffsetf = data.offsets[i];
            float4 gRf = data.rotations[i];
            float3 gS = data.scaling[i];
            float3 col = data.colors[i];

            Vector3 fT = new Vector3(T[idf].x,T[idf].y,T[idf].z);
            Vector3 gOffset = new Vector3(gOffsetf.x,gOffsetf.y,gOffsetf.z);

            Quaternion fR = new Quaternion(R[idf].x,R[idf].y,R[idf].z,R[idf].w);
            Quaternion gR = new Quaternion(gRf.x,gRf.y,gRf.z,gRf.w);

            Vector3 position = fT + fR * gOffset * k[idf];
            Quaternion rotation = fR * gR;
            Vector3 scale = Activate(gS) * k[idf];

            Color color = new Color(col.x, col.y, col.z);

            CreateGS(position, rotation, scale, color);
        }

    }

    void CreateGS(Vector3 pos, Quaternion rot, Vector3 scale, Color color)
    {
        GameObject gs = GameObject.CreatePrimitive(PrimitiveType.Sphere);

        gs.transform.SetParent(transform); // sets this current transform as parent

        gs.transform.localPosition = pos;
        gs.transform.localRotation = rot;
        gs.transform.localScale = scale;

        Renderer gsRenderer = gs.GetComponent<Renderer>();

        if (gsRenderer != null)
        {
            gsRenderer.material.color = color;
        }
    }

    void DestroyGaussians()
    {
        while(transform.childCount > 0) // for some reason the foreach doesn't get everything on the first try
        {
            foreach (Transform child in transform)
            {
                GameObject.DestroyImmediate(child.gameObject);
            }
        }
    }

    Vector3 Activate(float3 s)
    {
        // copied from hlsl code
        Debug.Log(s);
        s = 0.0f + math.saturate(1.0f / (1.0f + math.exp(-s))) * (0.1f - 0.0f);
        return new Vector3(s.x,s.y,s.z);
    }

    float4 MatrixToQuaternion(float3x3 m)
    {
        float4 q;
        float trace = m.c0.x + m.c1.y + m.c2.z; // Sum of diagonal elements

        float[] qAbs = new float[4];
        qAbs[0] = math.sqrt(math.max(0.0f, 1.0f + m.c0.x + m.c1.y + m.c2.z));
        qAbs[1] = math.sqrt(math.max(0.0f, 1.0f + m.c0.x - m.c1.y - m.c2.z));
        qAbs[2] = math.sqrt(math.max(0.0f, 1.0f - m.c0.x + m.c1.y - m.c2.z));
        qAbs[3] = math.sqrt(math.max(0.0f, 1.0f - m.c0.x - m.c1.y + m.c2.z));

        float4[] quatCandidates = new float4[4];
        quatCandidates[0] = new float4(
            qAbs[0] * 0.5f,
            (m.c2.y - m.c1.z) / (2.0f * qAbs[0]),
            (m.c0.z - m.c2.x) / (2.0f * qAbs[0]),
            (m.c1.x - m.c0.y) / (2.0f * qAbs[0])
        );

        quatCandidates[1] = new float4(
            (m.c2.y - m.c1.z) / (2.0f * qAbs[1]),
            qAbs[1] * 0.5f,
            (m.c1.x + m.c0.y) / (2.0f * qAbs[1]),
            (m.c0.z + m.c2.x) / (2.0f * qAbs[1])
        );

        quatCandidates[2] = new float4(
            (m.c0.z - m.c2.x) / (2.0f * qAbs[2]),
            (m.c1.x + m.c0.y) / (2.0f * qAbs[2]),
            qAbs[2] * 0.5f,
            (m.c2.y + m.c1.z) / (2.0f * qAbs[2])
        );

        quatCandidates[3] = new float4(
            (m.c1.x - m.c0.y) / (2.0f * qAbs[3]),
            (m.c0.z + m.c2.x) / (2.0f * qAbs[3]),
            (m.c2.y + m.c1.z) / (2.0f * qAbs[3]),
            qAbs[3] * 0.5f
        );

        // Find the best quaternion candidate (numerically stable, largest qAbs)
        int maxIndex = 0;
        float maxValue = qAbs[0];
        for (int i = 1; i < 4; i++)
        {
            if (qAbs[i] > maxValue)
            {
                maxIndex = i;
                maxValue = qAbs[i];
            }
        }

        q = quatCandidates[maxIndex];
        return math.normalize(q);
    }

}
