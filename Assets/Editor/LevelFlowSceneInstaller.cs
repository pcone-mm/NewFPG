using NewFPG.Level;
using NewFPG.Prototype;
using UnityEditor;
using UnityEditor.SceneManagement;
using UnityEngine;

namespace NewFPG.EditorTools
{
    public static class LevelFlowSceneInstaller
    {
        [MenuItem("NewFPG/Level/Install Underground First Floor Prototype")]
        public static void InstallUndergroundFirstFloorPrototype()
        {
            GameObject directorObject = GameObject.Find("LevelFlowDirector");
            if (directorObject == null)
            {
                directorObject = new GameObject("LevelFlowDirector");
                Undo.RegisterCreatedObjectUndo(directorObject, "Create Level Flow Director");
            }

            LevelFlowDirector director = directorObject.GetComponent<LevelFlowDirector>();
            if (director == null)
            {
                director = Undo.AddComponent<LevelFlowDirector>(directorObject);
            }

            PrototypeFirstPersonWeaponView weaponView = Object.FindFirstObjectByType<PrototypeFirstPersonWeaponView>();
            if (weaponView != null && weaponView.GetComponent<LevelWeaponProjectileShooter>() == null)
            {
                Undo.AddComponent<LevelWeaponProjectileShooter>(weaponView.gameObject);
            }

            EditorUtility.SetDirty(director);
            EditorSceneManager.MarkSceneDirty(directorObject.scene);
            EditorSceneManager.SaveScene(directorObject.scene);
            Debug.Log("Installed Underground First Floor level prototype in scene: " + directorObject.scene.path, directorObject);
        }

        [MenuItem("NewFPG/Level/Runtime Probe/Select First Choice")]
        public static void RuntimeProbeSelectFirstChoice()
        {
            LevelFlowDirector director = Object.FindFirstObjectByType<LevelFlowDirector>();
            bool selected = director != null && director.SelectChoice(0);
            Debug.Log(FormatProbe("SelectFirstChoice", director, selected), director);
        }

        [MenuItem("NewFPG/Level/Runtime Probe/Kill Active Enemies")]
        public static void RuntimeProbeKillActiveEnemies()
        {
            LevelFlowDirector director = Object.FindFirstObjectByType<LevelFlowDirector>();
            if (director != null)
            {
                director.DebugKillActiveEnemies();
            }

            Debug.Log(FormatProbe("KillActiveEnemies", director, director != null), director);
        }

        [MenuItem("NewFPG/Level/Runtime Probe/Select First Door")]
        public static void RuntimeProbeSelectFirstDoor()
        {
            LevelFlowDirector director = Object.FindFirstObjectByType<LevelFlowDirector>();
            bool selected = director != null && director.SelectDoor(0);
            Debug.Log(FormatProbe("SelectFirstDoor", director, selected), director);
        }

        [MenuItem("NewFPG/Level/Runtime Probe/Print State")]
        public static void RuntimeProbePrintState()
        {
            LevelFlowDirector director = Object.FindFirstObjectByType<LevelFlowDirector>();
            Debug.Log(FormatProbe("PrintState", director, director != null), director);
        }

        private static string FormatProbe(string action, LevelFlowDirector director, bool result)
        {
            if (director == null)
            {
                return "[LevelFlowProbe] " + action + " result=" + result + " director=null";
            }

            string roomId = director.CurrentRoom != null ? director.CurrentRoom.roomId : "null";
            return "[LevelFlowProbe] "
                + action
                + " result=" + result
                + " state=" + director.State
                + " room=" + roomId
                + " enemies=" + director.GetActiveEnemyCount()
                + " gold=" + director.Gold
                + " damageBonus=" + director.DamageBonus.ToString("0.##");
        }
    }
}
