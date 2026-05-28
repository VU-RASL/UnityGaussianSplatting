using UnityEngine;

[ExecuteAlways]
[DefaultExecutionOrder(-100)]
public class SMPLXGenderSelector : MonoBehaviour
{
    public enum AvatarGender
    {
        Male,
        Female
    }

    public AvatarGender gender = AvatarGender.Male;
    [SerializeField] private GameObject maleMesh;
    [SerializeField] private GameObject femaleMesh;
    [SerializeField] private PoseController poseController;

    void Reset()
    {
        AutoAssignReferences();
        ApplySelection();
    }

    void OnValidate()
    {
        AutoAssignReferences();
        ApplySelection();
    }

    void Awake()
    {
        AutoAssignReferences();
        ApplySelection();
    }

    void AutoAssignReferences()
    {
        if (poseController == null)
        {
            poseController = GetComponent<PoseController>();
        }

        Transform root = transform.parent != null ? transform.parent : transform;
        if (maleMesh == null)
        {
            maleMesh = FindChildByName(root, "update_uvmap_MALE");
        }

        if (femaleMesh == null)
        {
            femaleMesh = FindChildByName(root, "test_female");
        }
    }

    void ApplySelection()
    {
        bool useMale = gender == AvatarGender.Male;
        SetMeshState(maleMesh, useMale, SMPLX.ModelType.Male);
        SetMeshState(femaleMesh, !useMale, SMPLX.ModelType.Female);

        SMPLX selectedSmplx = GetSelectedSmplx();
        if (poseController != null && selectedSmplx != null)
        {
            poseController.smplx = selectedSmplx;
        }
    }

    SMPLX GetSelectedSmplx()
    {
        GameObject selectedMesh = gender == AvatarGender.Male ? maleMesh : femaleMesh;
        return selectedMesh != null ? selectedMesh.GetComponent<SMPLX>() : null;
    }

    static void SetMeshState(GameObject mesh, bool active, SMPLX.ModelType modelType)
    {
        if (mesh == null)
        {
            return;
        }

        SMPLX smplx = mesh.GetComponent<SMPLX>();
        if (smplx != null)
        {
            smplx.modelType = modelType;
        }

        if (mesh.activeSelf != active)
        {
            mesh.SetActive(active);
        }
    }

    static GameObject FindChildByName(Transform root, string childName)
    {
        Transform[] children = root.GetComponentsInChildren<Transform>(true);
        foreach (Transform child in children)
        {
            if (child.name == childName)
            {
                return child.gameObject;
            }
        }

        return null;
    }
}
