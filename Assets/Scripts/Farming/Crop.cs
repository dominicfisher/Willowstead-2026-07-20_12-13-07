using UnityEngine;

namespace Willowstead.Farming
{
    /// <summary>
    /// Represents an active crop in the world.
    /// Tracks growth stages and visual updates.
    /// </summary>
    [RequireComponent(typeof(SpriteRenderer))]
    public class Crop : MonoBehaviour
    {
        private CropData _cropData;
        private Vector3Int _gridPosition;
        private SpriteRenderer _spriteRenderer;
        private int _currentStage;
        private int _daysInCurrentStage;

        private System.Collections.Generic.List<SpriteRenderer> _childRenderers = new System.Collections.Generic.List<SpriteRenderer>();
        private int _visualsCount = 1;
        private Vector3[] _originalChildPositions;

        public CropData Data => _cropData;
        public Vector3Int GridPosition => _gridPosition;
        public int CurrentStage => _currentStage;
        public int VisualsCount => _visualsCount;

        /// <summary>
        /// True if the crop has reached its final growth stage.
        /// </summary>
        public bool IsMature => _cropData != null && _currentStage >= _cropData.TotalStages - 1;

        /// <summary>
        /// Snap the crop straight to a saved growth stage without playing
        /// the pop-up animation. Used only by SaveGameManager to restore
        /// worlds exactly where they left off.
        /// </summary>
        public void ForceStage(int stage)
        {
            ForceStage(stage, overrideVisualsCount: -1);
        }

        /// <summary>
        /// Restore a crop to a saved stage + (optionally) saved visuals
        /// count. Pass overrideVisualsCount &gt;= 1 to undo the random
        /// re-roll inside <see cref="Initialize"/>; otherwise the saved
        /// visualsCount is ignored and the crop relayouts itself. Skips
        /// the bounce-in coroutine on the first frame so a save-load
        /// doesn't pop every crop visibly.
        /// </summary>
        public void ForceStage(int stage, int overrideVisualsCount)
        {
            if (_cropData == null) return;
            if (overrideVisualsCount >= 1)
            {
                _visualsCount = Mathf.Max(1, overrideVisualsCount);
                // Rebuild visual layout so the saved spread/radius sticks.
                SetupRenderers();
            }
            int max = Mathf.Max(0, _cropData.TotalStages - 1);
            _currentStage = Mathf.Clamp(stage, 0, max);
            _daysInCurrentStage = 0;
            UpdateSprite(silent: Willowstead.Persistence.SaveGameManager.IsLoadingFromSave);
        }

        /// <summary>
        /// Same as UpdateSprite, but the optional <paramref name="silent"/>
        /// flag suppresses the per-stage bounce-in pop-up animation. Used
        /// by <see cref="ForceStage"/> during save-load so the loaded
        /// world doesn't re-animate every crop.
        /// </summary>
        private void UpdateSprite(bool silent = false)
        {
            if (_cropData == null || _cropData.GrowthStageSprites == null) return;
            ApplySpriteToRenderers();
            if (!silent && gameObject.activeInHierarchy)
            {
                if (_activeAnimation != null) StopCoroutine(_activeAnimation);
                _activeAnimation = StartCoroutine(PlayPopUpAnimation());
            }
        }

        private void ApplySpriteToRenderers()
        {
            if (_cropData == null || _cropData.GrowthStageSprites == null) return;
            if (_currentStage < 0 || _currentStage >= _cropData.TotalStages) return;
            Sprite stageSprite = _cropData.GrowthStageSprites[_currentStage];

            if (_visualsCount > 1)
            {
                for (int i = 0; i < _childRenderers.Count; i++)
                {
                    SpriteRenderer childSr = _childRenderers[i];
                    if (childSr == null) continue;
                    if (_currentStage == 0)
                    {
                        if (i == 0) { childSr.transform.localPosition = Vector3.zero; childSr.sprite = stageSprite; childSr.enabled = true; }
                        else childSr.enabled = false;
                    }
                    else
                    {
                        if (_originalChildPositions != null && i < _originalChildPositions.Length)
                            childSr.transform.localPosition = _originalChildPositions[i];
                        childSr.sprite = stageSprite;
                        childSr.enabled = true;
                    }
                }
            }
            else
            {
                foreach (var renderer in _childRenderers)
                {
                    if (renderer != null) renderer.sprite = stageSprite;
                }
            }
        }

        private void Awake()
        {
            _spriteRenderer = GetComponent<SpriteRenderer>();
        }

        /// <summary>
        /// Initializes the crop with data and grid coordinates.
        /// </summary>
        public void Initialize(CropData data, Vector3Int gridPosition)
        {
            _cropData = data;
            _gridPosition = gridPosition;
            _currentStage = 0;
            _daysInCurrentStage = 0;

            // Roll random visual crop count (safeguarding against 0 values from uninitialized asset files)
            int min = (_cropData != null && _cropData.MinVisualsPerTile > 0) ? _cropData.MinVisualsPerTile : 1;
            int max = (_cropData != null && _cropData.MaxVisualsPerTile > 0) ? _cropData.MaxVisualsPerTile : 4;
            _visualsCount = UnityEngine.Random.Range(min, max + 1);

            SetupRenderers();
            UpdateSprite();
        }

        private void SetupRenderers()
        {
            foreach (var r in _childRenderers)
            {
                if (r != null && r.gameObject != gameObject)
                {
                    Destroy(r.gameObject);
                }
            }
            _childRenderers.Clear();

            if (_cropData == null) return;

            if (_visualsCount <= 1)
            {
                if (_spriteRenderer != null)
                {
                    _childRenderers.Add(_spriteRenderer);
                    _spriteRenderer.enabled = true;
                    _spriteRenderer.sortingOrder = Mathf.RoundToInt(-transform.position.y * 100);
                }
            }
            else
            {
                if (_spriteRenderer != null)
                {
                    _spriteRenderer.enabled = false;
                }

                int count = _visualsCount;
                _originalChildPositions = new Vector3[count];
                for (int i = 0; i < count; i++)
                {
                    GameObject child = new GameObject($"CropVisual_{i}");
                    child.transform.SetParent(transform);

                    Vector3 offset = Vector3.zero;
                    if (_cropData.LayoutMode == CropLayoutMode.RowAligned)
                    {
                        int numCols = 3;
                        int colIndex = i % numCols;
                        float xPos = -0.2f + (colIndex * 0.2f); // -0.2f, 0.0f, 0.2f
                        
                        int rowIndexInCol = i / numCols;
                        int maxRowsInCol = (count - 1) / numCols + 1;
                        
                        float yMin = -0.18f;
                        float yMax = 0.18f;
                        float yPos = 0f;
                        
                        if (maxRowsInCol > 1)
                        {
                            float t = (float)rowIndexInCol / (maxRowsInCol - 1);
                            yPos = Mathf.Lerp(yMin, yMax, t);
                        }
                        else
                        {
                            yPos = Random.Range(-0.08f, 0.08f);
                        }

                        offset = new Vector3(xPos, yPos, 0f);
                    }
                    else
                    {
                        if (count == 2)
                        {
                            float angle = (i == 0) ? -45f : 135f;
                            float rad = angle * Mathf.Deg2Rad;
                            offset = new Vector3(Mathf.Cos(rad) * 0.15f, Mathf.Sin(rad) * 0.15f, 0f);
                        }
                        else if (count == 3)
                        {
                            float angle = i * 120f - 30f;
                            float rad = angle * Mathf.Deg2Rad;
                            offset = new Vector3(Mathf.Cos(rad) * 0.18f, Mathf.Sin(rad) * 0.18f, 0f);
                        }
                        else if (count == 4)
                        {
                            float angle = i * 90f + 45f;
                            float rad = angle * Mathf.Deg2Rad;
                            offset = new Vector3(Mathf.Cos(rad) * 0.18f, Mathf.Sin(rad) * 0.18f, 0f);
                        }
                        else
                        {
                            float angle = (i * 2.0f * Mathf.PI) / count;
                            float distance = _cropData.ScatterRadius * 0.8f;
                            offset = new Vector3(Mathf.Cos(angle) * distance, Mathf.Sin(angle) * distance, 0f);
                        }
                    }

                    float jitter = _cropData.RandomJitter;
                    offset += new Vector3(Random.Range(-jitter, jitter), Random.Range(-jitter, jitter), 0f);

                    child.transform.localPosition = offset;
                    _originalChildPositions[i] = offset;

                    float scale = Random.Range(_cropData.MinScale, _cropData.MaxScale);
                    child.transform.localScale = new Vector3(scale, scale, 1f);

                    SpriteRenderer childSr = child.AddComponent<SpriteRenderer>();
                    if (_spriteRenderer != null)
                    {
                        childSr.sharedMaterial = _spriteRenderer.sharedMaterial;
                        childSr.sortingLayerID = _spriteRenderer.sortingLayerID;
                        childSr.sortingLayerName = _spriteRenderer.sortingLayerName;
                        childSr.color = _spriteRenderer.color;
                    }

                    if (_cropData.AllowHorizontalFlip)
                    {
                        childSr.flipX = Random.value > 0.5f;
                    }

                    childSr.sortingOrder = Mathf.RoundToInt(-child.transform.position.y * 100);

                    _childRenderers.Add(childSr);
                }
            }
        }

        /// <summary>
        /// Advances the crop growth by one day.
        /// </summary>
        /// <param name="isWatered">Whether the tile was watered today.</param>
        public void Grow(bool isWatered)
        {
            if (IsMature) return; // Already fully grown

            if (isWatered)
            {
                _daysInCurrentStage++;
                
                if (_daysInCurrentStage >= _cropData.DaysPerStage)
                {
                    _currentStage++;
                    _daysInCurrentStage = 0;
                    UpdateSprite();
                    
#if UNITY_EDITOR
                    Debug.Log($"[Crop] {_cropData.CropName} grew to stage {_currentStage} at {_gridPosition}");
#endif
                }
            }
        }

        private bool _isHarvesting = false;

        /// <summary>
        /// Harvests the crop, clearing it from the world and returning the yield quantity.
        /// </summary>
        public int Harvest()
        {
            if (!IsMature)
            {
                Debug.LogWarning($"[Crop] Cannot harvest {_cropData.CropName} yet; it is not mature.");
                return 0;
            }

            if (_isHarvesting) return 0;
            _isHarvesting = true;

            int count = Mathf.Max(1, _visualsCount) * _cropData.YieldCount;
            string itemName = _cropData.YieldItemName;
            
#if UNITY_EDITOR
            Debug.Log($"[Crop] Harvested {count}x {itemName}!");
#endif

            // Tell GridManager we are gone immediately so the cell space is cleared
            World.GridManager.Instance.RemoveCrop(_gridPosition);

            StartCoroutine(PlayHarvestAnimationAndDestroy());

            return count;
        }

        private System.Collections.IEnumerator PlayHarvestAnimationAndDestroy()
        {
            float popDuration = 0.16f; // How long each individual crop takes to jump and shrink
            float delayBetweenPops = 0.06f; // Delay before starting the next crop pop

            int count = _childRenderers.Count;

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer childSr = _childRenderers[i];
                if (childSr == null) continue;

                StartCoroutine(AnimateSingleCropHarvest(childSr, popDuration));
                yield return new WaitForSeconds(delayBetweenPops);
            }

            // Wait for the final pop to finish before destroying the parent object
            yield return new WaitForSeconds(popDuration + 0.04f);

            Destroy(gameObject);
        }

        private System.Collections.IEnumerator AnimateSingleCropHarvest(SpriteRenderer childSr, float duration)
        {
            float elapsed = 0f;
            Transform childTransform = childSr.transform;
            Vector3 originalScale = childTransform.localScale;
            Vector3 originalLocalPos = childTransform.localPosition;
            Quaternion originalRotation = childTransform.localRotation;

            float jumpDirectionX = Random.Range(-0.06f, 0.06f);

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;

                float angle = Mathf.Sin(percent * Mathf.PI * 3f) * 12f;
                childTransform.localRotation = originalRotation * Quaternion.Euler(0f, 0f, angle);

                float yOffset = Mathf.Lerp(0f, 0.25f, percent);
                float xOffset = Mathf.Lerp(0f, jumpDirectionX, percent);
                childTransform.localPosition = new Vector3(originalLocalPos.x + xOffset, originalLocalPos.y + yOffset, originalLocalPos.z);

                float scaleMultiplier = 1f;
                if (percent < 0.4f)
                {
                    scaleMultiplier = Mathf.Lerp(1f, 1.25f, percent / 0.4f);
                }
                else
                {
                    scaleMultiplier = Mathf.Lerp(1.25f, 1.0f, (percent - 0.4f) / 0.6f);
                }
                childTransform.localScale = originalScale * scaleMultiplier;

                yield return null;
            }

            Player.HotbarUI hotbar = FindAnyObjectByType<Player.HotbarUI>();
            string yieldItemName = _cropData.YieldItemName;
            RectTransform targetSlot = (hotbar != null) ? hotbar.GetSlotRectForItem(yieldItemName) : null;
            int singleYield = _cropData.YieldCount;
            Player.InventoryManager inventory = FindAnyObjectByType<Player.InventoryManager>();

            World.FlyingItemAnimation.Spawn(childSr.sprite, childSr.transform.position, targetSlot, () =>
            {
                if (inventory != null)
                {
                    inventory.AddItem(yieldItemName, singleYield);
                }
                if (hotbar != null)
                {
                    hotbar.PulseCarrotSlot();
                }
            });

            Destroy(childSr.gameObject);
        }

        private Coroutine _activeAnimation;

        private void UpdateSprite() => UpdateSprite(silent: false);

        private System.Collections.IEnumerator PlayPopUpAnimation()
        {
            float duration = 0.28f;
            float elapsed = 0f;
            
            int count = _childRenderers.Count;
            Vector3[] targetLocalPositions = new Vector3[count];
            Vector3[] targetScales = new Vector3[count];

            for (int i = 0; i < count; i++)
            {
                if (_childRenderers[i] != null)
                {
                    targetLocalPositions[i] = _childRenderers[i].transform.localPosition;
                    targetScales[i] = _childRenderers[i].transform.localScale;
                }
            }

            while (elapsed < duration)
            {
                elapsed += Time.deltaTime;
                float percent = elapsed / duration;

                float scaleMultiplier = Mathf.Sin(percent * Mathf.PI * 0.5f); // ease out
                float bounce = Mathf.Sin(percent * Mathf.PI) * 0.15f * (1f - percent); // bounce factor
                float currentMultiplier = scaleMultiplier + bounce;

                float yOffset = Mathf.Lerp(-0.1f, 0f, percent);

                for (int i = 0; i < count; i++)
                {
                    SpriteRenderer childSr = _childRenderers[i];
                    if (childSr == null) continue;

                    Transform childTransform = childSr.transform;
                    
                    childTransform.localScale = targetScales[i] * currentMultiplier;

                    Vector3 targetPos = targetLocalPositions[i];
                    childTransform.localPosition = new Vector3(targetPos.x, targetPos.y + yOffset * currentMultiplier, targetPos.z);
                }

                yield return null;
            }

            for (int i = 0; i < count; i++)
            {
                SpriteRenderer childSr = _childRenderers[i];
                if (childSr == null) continue;
                
                childSr.transform.localScale = targetScales[i];
                childSr.transform.localPosition = targetLocalPositions[i];
            }
        }
    }
}
