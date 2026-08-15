#if UNITY_EDITOR
using UnityEditor;
using UnityEngine;

namespace Willowstead.Player.Editor
{
    /// <summary>
    /// Editor utility script that allows creating or setting up the permanent Player GameObject in the scene
    /// directly in Edit mode so that all player components and inspector settings are permanently saved to the scene.
    /// </summary>
    public static class PlayerSetupUtility
    {
        [MenuItem("Willowstead/Setup Permanent Player in Scene", false, 10)]
        public static void CreateOrSetupPlayerInScene()
        {
            GameObject playerGo = GameObject.FindWithTag("Player");
            if (playerGo == null) playerGo = GameObject.Find("Player");

            if (playerGo == null)
            {
                playerGo = new GameObject("Player");
                playerGo.transform.position = Vector3.zero;
                Undo.RegisterCreatedObjectUndo(playerGo, "Create Permanent Player");
            }

            try { playerGo.tag = "Player"; } catch {}

            SpriteRenderer sr = playerGo.GetComponent<SpriteRenderer>();
            if (sr == null) sr = Undo.AddComponent<SpriteRenderer>(playerGo);
            if (sr.sprite == null) sr.sprite = UIResourceHelper.GetCircleSprite();
            sr.color = new Color(0.95f, 0.85f, 0.55f, 1f);
            sr.sortingOrder = 50;

            CircleCollider2D col = playerGo.GetComponent<CircleCollider2D>();
            if (col == null) col = Undo.AddComponent<CircleCollider2D>(playerGo);
            col.radius = 0.4f;

            Rigidbody2D rb = playerGo.GetComponent<Rigidbody2D>();
            if (rb == null) rb = Undo.AddComponent<Rigidbody2D>(playerGo);
            rb.gravityScale = 0f;
            rb.freezeRotation = true;
            rb.interpolation = RigidbodyInterpolation2D.Interpolate;

            AudioSource audio = playerGo.GetComponent<AudioSource>();
            if (audio == null) audio = Undo.AddComponent<AudioSource>(playerGo);
            audio.playOnAwake = false;

            if (playerGo.GetComponent<InventoryManager>() == null) Undo.AddComponent<InventoryManager>(playerGo);
            if (playerGo.GetComponent<Farming.FarmingController>() == null) Undo.AddComponent<Farming.FarmingController>(playerGo);
            if (playerGo.GetComponent<HotbarUI>() == null) Undo.AddComponent<HotbarUI>(playerGo);
            if (playerGo.GetComponent<InventoryUI>() == null) Undo.AddComponent<InventoryUI>(playerGo);
            if (playerGo.GetComponent<ShopUI>() == null) Undo.AddComponent<ShopUI>(playerGo);
            if (playerGo.GetComponent<PlayerController>() == null) Undo.AddComponent<PlayerController>(playerGo);

            Selection.activeGameObject = playerGo;
            EditorUtility.SetDirty(playerGo);
            if (playerGo.scene.IsValid())
            {
                UnityEditor.SceneManagement.EditorSceneManager.MarkSceneDirty(playerGo.scene);
            }

            Debug.Log("[PlayerSetupUtility] Permanent Player GameObject setup in scene! Press Ctrl+S in Unity to save your scene.");
        }
    }
}
#endif
