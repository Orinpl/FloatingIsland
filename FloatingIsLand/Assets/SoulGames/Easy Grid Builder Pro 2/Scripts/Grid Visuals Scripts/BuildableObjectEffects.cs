using System;
using System.Collections;
using System.Collections.Generic;
using UnityEngine;

namespace SoulGames.EasyGridBuilderPro
{
    [AddComponentMenu("Easy Grid Builder Pro/Grid Effects/Buildable Object Effects", 0)]
    public class BuildableObjectEffects : MonoBehaviour
    {
        [Header("Spawn Effects")]
        [Space]
        [SerializeField] private bool useScalingEffectOnSpawn;
        [SerializeField] private bool blockScallingEffectForGhostObject = true;
        [SerializeField] private bool scalingAffectXAxis = true;
        [SerializeField] private bool scalingAffectYAxis = true;
        [SerializeField] private bool scalingAffectZAxis = true;
        [SerializeField] private AnimationCurve scaleEffectAnimationCurve = AnimationCurve.Linear(0, 0, 1, 1);
        [SerializeField] private Vector3 targetScale = Vector3.one;
        
        [Space]
        [SerializeField] private bool usePositionEffectOnSpawn;
        [SerializeField] private bool blockPositionEffectForGhostObject = true;
        [SerializeField] private bool PositioningAffectXAxis = true;
        [SerializeField] private bool PositioningAffectYAxis = true;
        [SerializeField] private bool PositioningAffectZAxis = true;
        [SerializeField] private AnimationCurve positionEffectAnimationCurve = AnimationCurve.Linear(0, 0, 1, 1);

        [Space]
        [SerializeField]public bool useSpawnGameObjectsOnSpawn;
        [Serializable]
        public class SpawnGameObjectOnSpawnProperties
        {
            public List<GameObject> spawnGameObjectList;
            public bool preventSpawnInGhostMode = true;
            public bool spawnRandomOneFromList;
            public Vector3 spawnLocalPosition = Vector3.zero;
            public Vector3 spawnLocalRotation = Vector3.zero;
            public Vector3 spawnLocalScale = Vector3.one;
            public float spawnnedObjectLifetime = 1f;
        }
        [SerializeField] private SpawnGameObjectOnSpawnProperties spawnGameObjectOnSpawnProperties;
        
        [Header("Destroy Effects")]
        [Space]
        [SerializeField] private bool useSpawnGameObjectsOnDestroy;
        [Serializable]
        public class SpawnGameObjectOnDestroyProperties
        {
            public List<GameObject> spawnGameObjectList;
            public bool spawnRandomOneFromList;
            public Vector3 spawnLocalPosition = Vector3.zero;
            public Vector3 spawnLocalRotation = Vector3.zero;
            public Vector3 spawnLocalScale = Vector3.one;
            public float spawnnedObjectLifetime = 1f;
        }
        [SerializeField] private SpawnGameObjectOnDestroyProperties spawnGameObjectOnDestroyProperties;


        [Header("Interaction Effects")]
        [Space]
        [SerializeField] private bool changeObjectMaterial = false;
        [SerializeField] private GameObject rootObject;
        [SerializeField] private List<GameObject> excludedChildObjects;
        [SerializeField] private Material baseMaterial;
        [SerializeField] private Material destroyerHoverChangeMaterial;
        [SerializeField] private Material selectorHoverChangeMaterial;
        [SerializeField] private Material selectorSelectedMaterial;
        [SerializeField] private Material moverHoverChangeMaterial;

        [Space]
        [SerializeField] private bool makeSoundEffect = false;
        [SerializeField] private AudioClip destroyerHoverEnterAudioClip;
        [SerializeField] private AudioClip destroyerHoverExitAudioClip;
        [SerializeField] private AudioClip selectorHoverEnterAudioClip;
        [SerializeField] private AudioClip selectorHoverExitAudioClip;
        [SerializeField] private AudioClip selectorSelectedAudioClip;
        [SerializeField] private AudioClip selectorDeselectedAudioClip;
        [SerializeField] private AudioClip moverHoverEnterAudioClip;
        [SerializeField] private AudioClip moverHoverExitAudioClip;
        [SerializeField] private AudioClip moverStartMovingAudioClip;
        [SerializeField] private AudioClip moverEndMovingAudioClip;

        private BuildableObject buildableObject;
        private AudioSource audioSource;
        private Vector3 startingPosition;
        private float scallingAnimationTime;
        private float positioningAnimationTime;
        private BuildableObjectDestroyer buildableObjectDestroyer;
        private BuildableObjectSelector buildableObjectSelector;
        private BuildableObjectMover buildableObjectMover;
        private const string GRID_AREA_VISUAL_GENERATOR_QUAD_NAME = "GridAreaVisualGeneratorQuad";

        private bool scallingEffectComplete;
        private bool positioningEffectComplete;
        private float effectCompleteTimeThreshold = 0.001f;

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE OBJECT EFFECTS INITIALIZE FUNCTIONS                                 ///
        ///-------------------------------------------------------------------------------///
        
        private void Start()
        {
            startingPosition = transform.position;

            if (!buildableObjectDestroyer) GridManager.Instance.TryGetBuildableObjectDestroyer(out buildableObjectDestroyer);
            buildableObjectDestroyer.OnBuildableObjectDestroyed += OnBuildableObjectDestroyed;

            if (!TryGetComponent<BuildableObject>(out buildableObject))
            {
                Debug.Log($"<b>GameObject: {gameObject.name}</b> <color=orange><b>Warning:</b> Buildable Object Component not found!</color> " +
                        $"Ensure the 'Buildable Object Effects' component is added to the root of the prefab.", gameObject);
            }

            SpawnObjectsOnSpawn();
        }

        private void OnEnable()
        {
            StartCoroutine(LateEnable());
        }

        private void OnDisable()
        {
            UnsubscribeFromEvents();
        }

        private void OnDestroy()
        {
            if (buildableObjectDestroyer) buildableObjectDestroyer.OnBuildableObjectDestroyed -= OnBuildableObjectDestroyed;
        }

        #region Buildable Object Effects Initialization Functions Start:
        private IEnumerator LateStart()
        {
            yield return new WaitForEndOfFrame();
            if (!GridManager.Instance.TryGetBuildableObjectDestroyer(out buildableObjectDestroyer)) yield break;
            buildableObjectDestroyer.OnBuildableObjectDestroyed += OnBuildableObjectDestroyed;
        }

        private void OnBuildableObjectDestroyed(EasyGridBuilderPro easyGridBuilderPro, BuildableObject buildableObject)
        {
            SpawnObjectsOnDestroy(buildableObject);
        }

        private IEnumerator LateEnable()
        {
            yield return new WaitForEndOfFrame();

            if (GridManager.Instance.TryGetBuildableObjectDestroyer(out buildableObjectDestroyer))
            {
                buildableObjectDestroyer.OnBuildableObjectHoverEnter += OnHoverEnterByBuildableObjectDestroyerDelegate;
                buildableObjectDestroyer.OnBuildableObjectHoverExit += OnHoverExitByBuildableObjectDestroyerDelegate;
            }

            if (GridManager.Instance.TryGetBuildableObjectSelector(out buildableObjectSelector))
            {
                buildableObjectSelector.OnBuildableObjectHoverEnter += OnHoverEnterByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectHoverExit += OnHoverExitByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectSelected += OnSelectedByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectDeselected += OnDeselectedByBuildableObjectSelectorDelegate;
            }

            if (GridManager.Instance.TryGetBuildableObjectMover(out buildableObjectMover))
            {
                buildableObjectMover.OnBuildableObjectHoverEnter += OnHoverEnterByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectHoverExit += OnHoverExitByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectStartMoving += OnBuildableObjectStartMovingByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectEndMoving += OnBuildableObjectEndMovingByBuildableObjectMoverDelegate;
            }
        }

        private void UnsubscribeFromEvents()
        {
            if (buildableObjectDestroyer)
            {
                buildableObjectDestroyer.OnBuildableObjectHoverEnter -= OnHoverEnterByBuildableObjectDestroyerDelegate;
                buildableObjectDestroyer.OnBuildableObjectHoverExit -= OnHoverExitByBuildableObjectDestroyerDelegate;
            }

            if (buildableObjectSelector)
            {
                buildableObjectSelector.OnBuildableObjectHoverEnter -= OnHoverEnterByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectHoverExit -= OnHoverExitByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectSelected -= OnSelectedByBuildableObjectSelectorDelegate;
                buildableObjectSelector.OnBuildableObjectDeselected -= OnDeselectedByBuildableObjectSelectorDelegate;
            }

            if (buildableObjectMover)
            {
                buildableObjectMover.OnBuildableObjectHoverEnter -= OnHoverEnterByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectHoverExit -= OnHoverExitByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectStartMoving -= OnBuildableObjectStartMovingByBuildableObjectMoverDelegate;
                buildableObjectMover.OnBuildableObjectEndMoving -= OnBuildableObjectEndMovingByBuildableObjectMoverDelegate;
            }
        }
        #endregion Buildable Object Effects Initialization Functions End:

        private void Update()
        {
            if (!buildableObject) return;

            if (useScalingEffectOnSpawn) ApplyScalingEffect();
            if (usePositionEffectOnSpawn) ApplyPositioningEffect();
        }

        ///-------------------------------------------------------------------------------///
        /// BUILDABLE OBJECT EVENT HANDLER FUNCTIONS                                      ///
        ///-------------------------------------------------------------------------------///
        
        #region Buildable Object Destroyer Events Start:
        private void OnHoverEnterByBuildableObjectDestroyerDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            ApplyDestroyerHoverChangeMaterial();
            PlayDestroyerHoverEnterSoundEffect();
        }

        private void OnHoverExitByBuildableObjectDestroyerDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            ApplyBaseMaterial();
            PlayDestroyerHoverExitSoundEffect();
        }
        #endregion Buildable Object Destroyer Events End:

        #region Buildable Object Selector Events Start:
        private void OnHoverEnterByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            ApplySelectorHoverChangeMaterial();
            PlaySelectorHoverEnterSoundEffect();
        }

        private void OnHoverExitByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            ApplyBaseMaterial();
            PlaySelectorHoverExitSoundEffect();
        }

        private void OnSelectedByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            ApplySelectorSelectedChangeMaterial();
            PlaySelectorSelectedSoundEffect();
        }

        private void OnDeselectedByBuildableObjectSelectorDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            ApplyBaseMaterial();
            PlaySelectorDeselectedSoundEffect();
        }
        #endregion Buildable Object Selector Events End:

        #region Buildable Object Mover Events Start:
        private void OnHoverEnterByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            ApplyMoverHoverChangeMaterial();
            PlayMoverHoverEnterSoundEffect();
        }

        private void OnHoverExitByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            ApplyBaseMaterial();
            PlayMoverHoverExitSoundEffect();
        }

        private void OnBuildableObjectStartMovingByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            PlayMoverStartMovingSoundEffect();
        }

        private void OnBuildableObjectEndMovingByBuildableObjectMoverDelegate(BuildableObject buildableObject)
        {
            if (buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;
            PlayMoverEndMovingSoundEffect();
        }
        #endregion Buildable Object Mover Events End:
  
        ///-------------------------------------------------------------------------------///
        /// HANDLE SPAWN SCALLING AND POSITIONING FUNCTIONS                               ///
        ///-------------------------------------------------------------------------------///

        #region Object Scalling Effects Functions Start:
        // Scales the object based on an animation curve over time.
        private void ApplyScalingEffect()
        {
            if (scallingEffectComplete) return;
            if (blockScallingEffectForGhostObject && buildableObject.GetIsInstantiatedByGhostObject()) return;

            scallingAnimationTime += Time.deltaTime;
            float scaleValue = scaleEffectAnimationCurve.Evaluate(scallingAnimationTime);
            transform.localScale = new Vector3(scalingAffectXAxis ? scaleValue : targetScale.x, scalingAffectYAxis ? scaleValue : targetScale.y, scalingAffectZAxis ? scaleValue : targetScale.z);

            // Check if the object has reached the target scale and if the component should be destroyed
            if (transform.localScale.Approximately(targetScale, effectCompleteTimeThreshold)) scallingEffectComplete = true;
        }
        #endregion Object Scalling Effects Functions End:

        #region Object Positioning Effects Functions Start:
        private void ApplyPositioningEffect()
        {
            if (positioningEffectComplete) return;
            if (blockPositionEffectForGhostObject && buildableObject.GetIsInstantiatedByGhostObject()) return;

            positioningAnimationTime += Time.deltaTime;
            float positionValue = positionEffectAnimationCurve.Evaluate(positioningAnimationTime);
            transform.localPosition = new Vector3(PositioningAffectXAxis ? startingPosition.x + positionValue : startingPosition.x, 
                                                PositioningAffectYAxis ? startingPosition.y + positionValue : startingPosition.y, 
                                                PositioningAffectZAxis ? startingPosition.z + positionValue : startingPosition.z);

            // Check if the object has reached the target scale and if the component should be destroyed
            if (transform.localPosition.Approximately(startingPosition, effectCompleteTimeThreshold)) positioningEffectComplete = true;
        }
        #endregion Object Positioning Effects Functions End:

        ///-------------------------------------------------------------------------------///
        /// HANDLE SPAWN OBJECTS ON SPAWN FUNCTIONS                                       ///
        ///-------------------------------------------------------------------------------///
        
        #region On Spawn, Spawn Objects Effects Functions Start:
        public void SpawnObjectsOnSpawn()
        {
            if (!useSpawnGameObjectsOnSpawn) return;
            if (spawnGameObjectOnSpawnProperties.preventSpawnInGhostMode && buildableObject.GetIsInstantiatedByGhostObject()) return;

            GameObject[] spawnnedObject;
            if (spawnGameObjectOnSpawnProperties.spawnRandomOneFromList)
            {
                GameObject randomObject = spawnGameObjectOnSpawnProperties.spawnGameObjectList[UnityEngine.Random.Range(0, spawnGameObjectOnSpawnProperties.spawnGameObjectList.Count)];
                spawnnedObject = new GameObject[1];
                spawnnedObject[0] = Instantiate(randomObject, transform.localPosition + spawnGameObjectOnSpawnProperties.spawnLocalPosition, Quaternion.identity);
                spawnnedObject[0].transform.eulerAngles = spawnGameObjectOnSpawnProperties.spawnLocalRotation;
                spawnnedObject[0].transform.localScale = spawnGameObjectOnSpawnProperties.spawnLocalScale;
                if (spawnGameObjectOnSpawnProperties.spawnnedObjectLifetime > 0) Destroy(spawnnedObject[0], spawnGameObjectOnSpawnProperties.spawnnedObjectLifetime);
            }
            else
            {
                spawnnedObject = new GameObject[spawnGameObjectOnSpawnProperties.spawnGameObjectList.Count];
                for (int i = 0; i < spawnGameObjectOnSpawnProperties.spawnGameObjectList.Count; i++)
                {
                    spawnnedObject[i] = Instantiate(spawnGameObjectOnSpawnProperties.spawnGameObjectList[i], transform.localPosition + spawnGameObjectOnSpawnProperties.spawnLocalPosition, Quaternion.identity);
                    spawnnedObject[i].transform.eulerAngles = spawnGameObjectOnSpawnProperties.spawnLocalRotation;
                    spawnnedObject[i].transform.localScale = spawnGameObjectOnSpawnProperties.spawnLocalScale;
                    if (spawnGameObjectOnSpawnProperties.spawnnedObjectLifetime > 0) Destroy(spawnnedObject[i], spawnGameObjectOnSpawnProperties.spawnnedObjectLifetime);
                }
            }
        }
        #endregion On Spawn, Objects Effects Functions End:

        ///-------------------------------------------------------------------------------///
        /// HANDLE SPAWN OBJECTS ON DESTROY FUNCTIONS                                     ///
        ///-------------------------------------------------------------------------------///
        
        #region On Destroy, Spawn Objects Effects Functions Start:
        public void SpawnObjectsOnDestroy(BuildableObject buildableObject)
        {
            if (!buildableObject) return;
            if (!useSpawnGameObjectsOnDestroy || buildableObject.GetUniqueID() != this.buildableObject.GetUniqueID()) return;

            GameObject[] spawnnedObject;
            if (spawnGameObjectOnDestroyProperties.spawnRandomOneFromList)
            {
                GameObject randomObject = spawnGameObjectOnDestroyProperties.spawnGameObjectList[UnityEngine.Random.Range(0, spawnGameObjectOnDestroyProperties.spawnGameObjectList.Count)];
                spawnnedObject = new GameObject[1];
                spawnnedObject[0] = Instantiate(randomObject, transform.localPosition + spawnGameObjectOnDestroyProperties.spawnLocalPosition, Quaternion.identity);
                spawnnedObject[0].transform.eulerAngles = spawnGameObjectOnDestroyProperties.spawnLocalRotation;
                spawnnedObject[0].transform.localScale = spawnGameObjectOnDestroyProperties.spawnLocalScale;
                if (spawnGameObjectOnDestroyProperties.spawnnedObjectLifetime > 0) Destroy(spawnnedObject[0], spawnGameObjectOnDestroyProperties.spawnnedObjectLifetime);
            }
            else
            {
                spawnnedObject = new GameObject[spawnGameObjectOnDestroyProperties.spawnGameObjectList.Count];
                for (int i = 0; i < spawnGameObjectOnDestroyProperties.spawnGameObjectList.Count; i++)
                {
                    spawnnedObject[i] = Instantiate(spawnGameObjectOnDestroyProperties.spawnGameObjectList[i], transform.localPosition + spawnGameObjectOnDestroyProperties.spawnLocalPosition, Quaternion.identity);
                    spawnnedObject[i].transform.eulerAngles = spawnGameObjectOnDestroyProperties.spawnLocalRotation;
                    spawnnedObject[i].transform.localScale = spawnGameObjectOnDestroyProperties.spawnLocalScale;
                    if (spawnGameObjectOnDestroyProperties.spawnnedObjectLifetime > 0) Destroy(spawnnedObject[i], spawnGameObjectOnDestroyProperties.spawnnedObjectLifetime);
                }
            }
        }
        #endregion On Destroy, Spawn Objects Effects Functions End:

        ///-------------------------------------------------------------------------------///
        /// HANDLE CHANGE MATERIAL FUNCTIONS                                              ///
        ///-------------------------------------------------------------------------------///

        #region Object Change Material Effects Functions Start:
        public void ApplyDestroyerHoverChangeMaterial()
        {
            if (changeObjectMaterial) ApplyMaterialToGameObjectAndChildren(rootObject, destroyerHoverChangeMaterial);
        }

        public void ApplySelectorHoverChangeMaterial()
        {
            if (changeObjectMaterial) ApplyMaterialToGameObjectAndChildren(rootObject, selectorHoverChangeMaterial);
        }

        public void ApplySelectorSelectedChangeMaterial()
        {
            if (changeObjectMaterial) ApplyMaterialToGameObjectAndChildren(rootObject, selectorSelectedMaterial);
        }

        public void ApplyMoverHoverChangeMaterial()
        {
            if (changeObjectMaterial) ApplyMaterialToGameObjectAndChildren(rootObject, moverHoverChangeMaterial);
        }

        public void ApplyBaseMaterial()
        {
            if (changeObjectMaterial) ApplyMaterialToGameObjectAndChildren(rootObject, baseMaterial);
        }

        private void ApplyMaterialToGameObjectAndChildren(GameObject gameObject, Material material)
        {
            if (!gameObject) gameObject = this.gameObject;
            
            // Check if the current object is in the excluded list
            if (!excludedChildObjects.Contains(gameObject) && gameObject.name != GRID_AREA_VISUAL_GENERATOR_QUAD_NAME)
            {
                // Apply the material if the object is not in the excluded list
                MeshRenderer renderer = gameObject.GetComponent<MeshRenderer>();
                if (renderer != null) renderer.sharedMaterial = material;

                // Recursively apply the material to all children, excluding specified ones
                foreach (Transform child in gameObject.transform)
                {
                    ApplyMaterialToGameObjectAndChildren(child.gameObject, material);
                }
            }
        }
        #endregion Object Change Material Effects Functions End:

        ///-------------------------------------------------------------------------------///
        /// HANDLE MAKE SOUND EFFECT FUNCTIONS                                            ///
        ///-------------------------------------------------------------------------------///
        
        #region Object Make Sound Effects Functions Start:
        public void PlayDestroyerHoverEnterSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(destroyerHoverEnterAudioClip);
        }

        public void PlayDestroyerHoverExitSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(destroyerHoverExitAudioClip);
        }

        public void PlaySelectorHoverEnterSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(selectorHoverEnterAudioClip);
        }

        public void PlaySelectorHoverExitSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(selectorHoverExitAudioClip);
        }

        public void PlaySelectorSelectedSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(selectorSelectedAudioClip);
        }

        public void PlaySelectorDeselectedSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(selectorDeselectedAudioClip);
        }

        public void PlayMoverHoverEnterSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(moverHoverEnterAudioClip);
        }

        public void PlayMoverHoverExitSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(moverHoverExitAudioClip);
        }

        public void PlayMoverStartMovingSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(moverStartMovingAudioClip);
        }

        public void PlayMoverEndMovingSoundEffect()
        {
            if (makeSoundEffect) PlayAudioClip(moverEndMovingAudioClip);
        }

        private void PlayAudioClip(AudioClip audioClip)
        {
            if (!audioSource) audioSource = gameObject.AddComponent<AudioSource>();
            audioSource.clip = audioClip;
            audioSource.Play();
        }
        #endregion Object Make Sound Effects Functions End:
    }

    public static class Vector3Extensions
    {
        // Extension method to compare two Vector3 values with a tolerance
        public static bool Approximately(this Vector3 a, Vector3 b, float tolerance = 0.001f)
        {
            return (a - b).sqrMagnitude < tolerance * tolerance;
        }
    }
}