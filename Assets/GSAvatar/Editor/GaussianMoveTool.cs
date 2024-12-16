// SPDX-License-Identifier: MIT

using GSAvatar.Runtime;
using UnityEditor;
using UnityEditor.EditorTools;
using UnityEngine;

namespace GSAvatar.Editor
{
    [EditorTool("Gaussian Move Tool", typeof(GSAvatarRenderer), typeof(GaussianToolContext))]
    class GaussianMoveTool : GaussianTool
    {
        public override void OnToolGUI(EditorWindow window)
        {
            var gs = GetRenderer();
            if (!gs || !CanBeEdited() || !HasSelection())
                return;
            var tr = gs.transform;

            EditorGUI.BeginChangeCheck();
            var selCenterLocal = GetSelectionCenterLocal();
            var selCenterWorld = tr.TransformPoint(selCenterLocal);
            var newPosWorld = Handles.DoPositionHandle(selCenterWorld, Tools.handleRotation);
            if (EditorGUI.EndChangeCheck())
            {
                var newPosLocal = tr.InverseTransformPoint(newPosWorld);
                var wasModified = gs.editModified;
                gs.EditTranslateSelection(newPosLocal - selCenterLocal);
                if (!wasModified)
                    GSAvatarRendererEditor.RepaintAll();
                Event.current.Use();
            }
        }
    }
}
