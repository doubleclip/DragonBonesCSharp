﻿using System.Collections.Generic;
using UnityEngine;
#if UNITY_EDITOR
using UnityEditor;
#endif

namespace DragonBones
{
    /**
     * @private
     */
    internal class ClockHandler : MonoBehaviour
    {
        void Update()
        {
            UnityFactory._clock.AdvanceTime(Time.deltaTime);
        }
    }
    /**
     * @language zh_CN
     * Unity 工厂。
     * @version DragonBones 3.0
     */
    public class UnityFactory : BaseFactory
    {
        private static IEventDispatcher<EventObject> _eventManager = null;
        private static GameObject _gameObject = null;
        /**
         * @private
         */
        internal static readonly WorldClock _clock = new WorldClock();
        /**
         * @language zh_CN
         * 一个可以直接使用的全局工厂实例。
         * @version DragonBones 4.7
         */
        public static readonly UnityFactory factory = new UnityFactory();
        /**
         * @language zh_CN
         * 创建材质时默认使用的 shader。
         * @version DragonBones 4.7
         */
        public string defaultShaderName = "Sprites/Default";
        public string defaultUIShaderName = "UI/Default";

        private string _textureAtlasPath = null;
        private GameObject _armatureGameObject = null;
        private bool _isUGUI = false;
        private readonly Dictionary<string, DragonBonesData> _pathDragonBonesDataMap = new Dictionary<string, DragonBonesData>();
        private readonly Dictionary<string, TextureAtlasData> _pathTextureAtlasDataMap = new Dictionary<string, TextureAtlasData>();
        /**
         * @language zh_CN
         * 创建一个工厂。 (通常只需要一个全局工厂实例)
         * @param dataParser 龙骨数据解析器，如果不设置，则使用默认解析器。
         * @version DragonBones 3.0
         */
        public UnityFactory(DataParser dataParser = null) : base(dataParser)
        {
        }
        /**
         * @private
         */
        override protected TextureAtlasData _BuildTextureAtlasData(TextureAtlasData textureAtlasData, object textureAtlas)
        {
            if (textureAtlasData != null)
            {
                if (textureAtlas != null)
                {
                    if ((textureAtlas as Material).name.IndexOf("UI_Mat") > -1)
                    {
                        (textureAtlasData as UnityTextureAtlasData).uiTexture = textureAtlas as Material;
                    }
                    else
                    {
                        (textureAtlasData as UnityTextureAtlasData).texture = textureAtlas as Material;
                    }
                }
            }
            else
            {
                textureAtlasData = BaseObject.BorrowObject<UnityTextureAtlasData>();
            }

            return textureAtlasData;
        }
        /**
         * @private
         */
        override protected Armature _BuildArmature(BuildArmaturePackage dataPackage)
        {
            if (Application.isPlaying) //
            {
                if (_gameObject == null)
                {
                    _gameObject = new GameObject("DragonBones Object", typeof(ClockHandler));
                    _gameObject.isStatic = true;
                    _gameObject.hideFlags = HideFlags.HideInHierarchy;
                }

                if (_eventManager == null)
                {
                    _eventManager = _gameObject.AddComponent<UnityArmatureComponent>();
                    (_eventManager as UnityArmatureComponent).isUGUI = _isUGUI;
                }
            }

            var armature = BaseObject.BorrowObject<Armature>();
            var armatureDisplay = _armatureGameObject == null ? new GameObject(dataPackage.armature.name) : _armatureGameObject;
            var armatureComponent = armatureDisplay.GetComponent<UnityArmatureComponent>();
            if (armatureComponent == null)
            {
                armatureComponent = armatureDisplay.AddComponent<UnityArmatureComponent>();
                armatureComponent.isUGUI = _isUGUI;
            }

            armatureComponent._armature = armature;

            armature.Init(dataPackage.armature, armatureComponent, armatureDisplay, this._dragonBones);

            _armatureGameObject = null;

            return armature;
        }
        /**
         * @private
         */
        override protected Slot _BuildSlot(BuildArmaturePackage dataPackage, SlotData slotData, List<DisplayData> displays, Armature armature)
        {
            var slot = BaseObject.BorrowObject<UnitySlot>();
            var displayList = new List<object>();
            displayList.ResizeList(displays.Count);

            var armatureDisplay = armature.display as GameObject;
            var slotsRoot = armatureDisplay.GetComponent<UnityArmatureComponent>().slotsRoot.transform;
            var transform = slotsRoot.Find(slot.name);
            var gameObject = transform == null ? null : transform.gameObject;
            if (gameObject == null)
            {
                gameObject = new GameObject(slotData.name);
            }

            slot.Init(slotData, displays, gameObject, gameObject);

            for (int i = 0, l = displays.Count; i < l; ++i)
            {
                var displayData = displays[i];
                switch (displayData.type)
                {
                    case DisplayType.Image:
                    case DisplayType.Mesh:
                        var imageDisplayData = displayData as ImageDisplayData;
                        if (imageDisplayData.texture == null)
                        {
                            imageDisplayData.texture = _GetTextureData(dataPackage.dataName, displayData.path);
                        }

                        if (!string.IsNullOrEmpty(dataPackage.textureAtlasName))
                        {
                            slot._displayDatas[i] = displayData;
                        }

                        displayList[i] = slot.rawDisplay;
                        break;
                    case DisplayType.Armature:
                        var armatureDisplayData = displayData as ArmatureDisplayData;
                        var childDisplayName = slotData.name + " (" + armatureDisplayData.path + ")"; //
                        var childTransform = slotsRoot.Find(childDisplayName);
                        var childArmature = childTransform == null ?
                            BuildArmature(armatureDisplayData.path, dataPackage.dataName) :
                            BuildArmatureComponent(armatureDisplayData.path, dataPackage.dataName, null, dataPackage.textureAtlasName, childTransform.gameObject).armature;

                        if (childArmature != null)
                        {
                            childArmature.inheritAnimation = armatureDisplayData.inheritAnimation;
                            if (!childArmature.inheritAnimation)
                            {
                                var actions = childArmature.armatureData.actions;
                                if (actions.Count > 0)
                                {
                                    foreach (var actionData in actions)
                                    {
                                        childArmature._BufferAction(actionData, true);
                                    }
                                }
                                else
                                {
                                    childArmature.animation.Play();
                                }
                            }

                            armatureDisplayData.armature = childArmature.armatureData; // 

                            // Hide
                            var childArmatureDisplay = childArmature.display as GameObject;
                            childArmatureDisplay.GetComponent<UnityArmatureComponent>().isUGUI = armatureDisplay.GetComponent<UnityArmatureComponent>().isUGUI;
                            childArmatureDisplay.name = childDisplayName;
                            childArmatureDisplay.gameObject.hideFlags = HideFlags.HideInHierarchy;
                            childArmatureDisplay.SetActive(false);
                        }

                        displayList[i] = childArmature;
                        break;
                    default:
                        displayList[i] = null;
                        break;
                }
            }

            slot._SetDisplayList(displayList);

            return slot;
        }
        /**
         * @private
         */
		protected void _RefreshTextureAtlas(UnityTextureAtlasData textureAtlasData,bool isUGUI,bool isEditor=false)
        {
			Material material = null;
			if(isUGUI && textureAtlasData.uiTexture == null)
            {
				if(isEditor)
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        material = AssetDatabase.LoadAssetAtPath<Material>(textureAtlasData.imagePath + "_UI_Mat.mat");
                    }
					#endif
				}
                else
                {
					material = Resources.Load<Material>(textureAtlasData.imagePath+"_UI_Mat");
				}
				if(material==null)
                {
					Texture2D textureAtlas = null;
					if(isEditor)
                    {
                        #if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                            textureAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAtlasData.imagePath + ".png");
                        }
						#endif
					}
                    else
                    {
						textureAtlas = Resources.Load<Texture2D>(textureAtlasData.imagePath);
					}

					Shader shader = Shader.Find(defaultUIShaderName);
					material = new Material(shader);
					material.name = textureAtlas.name+"_UI_Mat";
					material.mainTexture = textureAtlas;
					if(textureAtlasData.width<2) textureAtlasData.width = (uint)textureAtlas.width;
					if(textureAtlasData.height<2) textureAtlasData.height = (uint)textureAtlas.height;
					textureAtlasData._disposeTexture = true;
					#if UNITY_EDITOR
					if(!Application.isPlaying)
                    {
						string path = AssetDatabase.GetAssetPath(textureAtlas);
						path = path.Substring(0,path.Length-4);
						AssetDatabase.CreateAsset(material,path+"_UI_Mat.mat");
						AssetDatabase.SaveAssets();
					}
					#endif
				}
				textureAtlasData.uiTexture = material;
			}
			else if(!isUGUI && textureAtlasData.texture == null)
            {
				if(isEditor)
                {
                    #if UNITY_EDITOR
                    if (!Application.isPlaying)
                    {
                        material = AssetDatabase.LoadAssetAtPath<Material>(textureAtlasData.imagePath + "_Mat.mat");
                    }
					#endif
				}
                else
                {
					material = Resources.Load<Material>(textureAtlasData.imagePath+"_Mat");
				}
				if(material == null)
				{
					Texture2D textureAtlas = null;
					if(isEditor)
                    {
                        #if UNITY_EDITOR
                        if (!Application.isPlaying)
                        {
                            textureAtlas = AssetDatabase.LoadAssetAtPath<Texture2D>(textureAtlasData.imagePath + ".png");
                        }
						#endif
					}
                    else
                    {
						textureAtlas = Resources.Load<Texture2D>(textureAtlasData.imagePath);
					}

					Shader shader = Shader.Find(defaultShaderName);
					material = new Material(shader);
					material.name = textureAtlas.name + "_Mat";
					material.mainTexture = textureAtlas;
					if(textureAtlasData.width<2) textureAtlasData.width = (uint)textureAtlas.width;
					if(textureAtlasData.height<2) textureAtlasData.height = (uint)textureAtlas.height;
					textureAtlasData._disposeTexture = true;
					#if UNITY_EDITOR
					if(!Application.isPlaying)
                    {
						string path = AssetDatabase.GetAssetPath(textureAtlas);
						path = path.Substring(0,path.Length-4);
						AssetDatabase.CreateAsset(material,path+"_Mat.mat");
						AssetDatabase.SaveAssets();
					}
					#endif
				}

				textureAtlasData.texture = material;
			}
        }

        /**
         * @inheritDoc
         */
        public override void RemoveDragonBonesData(string name, bool disposeData = true)
        {
            var dragonBonesData = GetDragonBonesData(name);
            if (_pathDragonBonesDataMap.ContainsValue(dragonBonesData))
            {
                foreach (var pair in _pathDragonBonesDataMap)
                {
                    if (pair.Value == dragonBonesData)
                    {
                        _pathDragonBonesDataMap.Remove(pair.Key);
                        break;
                    }
                }
            }

            base.RemoveDragonBonesData(name, disposeData);
        }
        /**
         * @inheritDoc
         */
        public override void RemoveTextureAtlasData(string name, bool disposeData = true)
        {
            var textureAtlasDataList = GetTextureAtlasData(name);
            if (textureAtlasDataList != null)
            {
                foreach (var textureAtlasData in textureAtlasDataList)
                {
                    if (_pathTextureAtlasDataMap.ContainsValue(textureAtlasData))
                    {
                        foreach (var pair in _pathTextureAtlasDataMap)
                        {
                            if (pair.Value == textureAtlasData)
                            {
                                _pathTextureAtlasDataMap.Remove(pair.Key);
                                break;
                            }
                        }
                    }
                }
            }

            base.RemoveTextureAtlasData(name, disposeData);
        }
        /**
         * @inheritDoc
         */
        public override void Clear(bool disposeData = true)
        {
            base.Clear(disposeData);

            _pathDragonBonesDataMap.Clear();
            _pathTextureAtlasDataMap.Clear();
        }
        /**
         * @language zh_CN
         * 创建一个指定名称的骨架，并使用骨架的显示容器来更新骨架动画。
         * @param armatureName 骨架数据名称。
         * @param dragonBonesName 龙骨数据名称，如果未设置，将检索所有的龙骨数据，如果多个数据中包含同名的骨架数据，可能无法创建出准确的骨架。
         * @param skinName 皮肤名称，如果未设置，则使用默认皮肤。
         * @param isUGUI 是否是UGUI，默认为false
         * @returns 骨架的显示容器。
         * @see DragonBones.UnityArmatureComponent
         * @version DragonBones 4.5
         */
		public UnityArmatureComponent BuildArmatureComponent(string armatureName, string dragonBonesName = null, string skinName = null, string textureAtlasName = null, GameObject gameObject = null,bool isUGUI=false)
        {
            _armatureGameObject = gameObject;
			_isUGUI = isUGUI;
            var armature = BuildArmature(armatureName, dragonBonesName, skinName, textureAtlasName);
            if (armature != null)
            {
                _clock.Add(armature);

                var armatureDisplay = armature.display as GameObject;
                var armatureComponent = armatureDisplay.GetComponent<UnityArmatureComponent>();

                return armatureComponent;
            }

            return null;
        }
        /**
         * @language zh_CN
         * 获取带有指定贴图的显示对象。
         * @param textureName 指定的贴图名称。
         * @param textureAtlasName 指定的龙骨数据名称，如果未设置，将检索所有的龙骨数据。
         * @version DragonBones 3.0
         */
        public GameObject GetTextureDisplay(string textureName, string textureAtlasName = null)
        {
            /*var textureData = _getTextureData(textureAtlasName, textureName) as UnityTextureData;
            if (textureData != null)
            {
                if (textureData.texture == null)
                {
                    var textureAtlasTexture = (textureData.parent as UnityTextureAtlasData).texture;

                    var rect = new Rect(
                        textureData.region.x,
                        textureAtlasTexture.height - textureData.region.y - textureData.region.height,
                        textureData.region.width,
                        textureData.region.height
                    );

                    textureData.texture = Sprite.Create(textureAtlasTexture, rect, new Vector2(), 1.0f);
                }

                var gameObject = new GameObject();
                gameObject.AddComponent<SpriteRenderer>().sprite = textureData.texture;
                return gameObject;
            }*/

            return null;
        }
        /**
         * @language zh_CN
         * 获取全局声音事件管理器。
         * @version DragonBones 4.5
         */
        public IEventDispatcher<EventObject> soundEventManager
        {
            get { return _eventManager; }
        }

		/**
         * @language zh_CN
         * 解析龙骨数据。
         * @param data 龙骨数据
         * @param isUGUI 为数据提供一个名称，以便可以通过这个名称获取数据，如果未设置，则使用数据中的名称。
         * @param texScale 贴图缩放值
         * @returns 龙骨数据
         */
		public DragonBonesData LoadData(UnityDragonBonesData data,bool isUGUI=false,float texScale=0)
		{
			DragonBonesData dragonBonesData = null;

			if (data.dragonBonesJSON != null)
			{
				dragonBonesData = LoadDragonBonesData(data.dragonBonesJSON,data.dataName);

				if (!string.IsNullOrEmpty(data.dataName) && dragonBonesData != null && data.textureAtlas != null)
				{
					#if UNITY_EDITOR
					bool isDirty = false;
					if(!Application.isPlaying)
                    {
						for(int i=0;i<data.textureAtlas.Length;++i)
						{
							if(isUGUI)
                            {
								if(data.textureAtlas[i].uiMaterial==null)
                                {
									isDirty = true;
									break;
								}
							}
                            else
                            {
								if(data.textureAtlas[i].material==null)
                                {
									isDirty = true;
									break;
								}
							}
						}
					}
					#endif
					for(int i=0;i<data.textureAtlas.Length;++i)
					{
						LoadTextureAtlasData(data.textureAtlas[i],data.dataName,texScale,isUGUI);
					}
					#if UNITY_EDITOR
					if(isDirty)
                    {
						AssetDatabase.Refresh();
						EditorUtility.SetDirty(data);
						AssetDatabase.SaveAssets();
					}
					#endif
				}
			}

			return dragonBonesData;
		}


        /**
         * @language zh_CN
         * 加载、解析并添加龙骨数据。
         * @param path 龙骨数据在 Resources 中的路径。（其他形式的加载可自行扩展）
         * @param name 为数据提供一个名称，以便可以通过这个名称获取数据，如果未设置，则使用数据中的名称。
         * @param scale 为所有骨架设置一个缩放值。
         * @returns 龙骨数据
         * @see #ParseDragonBonesData()
         * @see #GetDragonBonesData()
         * @see #AddDragonBonesData()
         * @see #RemoveDragonBonesData()
         * @see DragonBones.DragonBonesData
         */
		[System.Obsolete("Use 'DragonBonesData LoadData(UnityDragonBonesData data)'")]
        public DragonBonesData LoadDragonBonesData(string path, string name = null, float scale = 0.01f)
        {
            var index = path.LastIndexOf("Resources");
            if (index > 0)
            {
                path = path.Substring(index + 10);
            }

            index = path.LastIndexOf(".");
            if (index > 0)
            {
                path = path.Substring(0, index);
            }

            if (_pathDragonBonesDataMap.ContainsKey(path))
            {
                return _pathDragonBonesDataMap[path];
            }

            var dragonBonesData = LoadDragonBonesData(Resources.Load<TextAsset>(path), name);
            if (dragonBonesData != null)
            {
				_pathDragonBonesDataMap[path] = dragonBonesData;
            }

            return dragonBonesData;
        }
        /**
         * @private
         */
		protected DragonBonesData LoadDragonBonesData(TextAsset dragonBonesJSON, string name = null, float scale = 0.01f)
        {
            if (dragonBonesJSON == null)
            {
                return null;
            }

            if (!string.IsNullOrEmpty(name))
            {
                var existedData = GetDragonBonesData(name);
                if (existedData != null)
                {
                    return existedData;
                }
            }

			DragonBonesData data = ParseDragonBonesData((Dictionary<string, object>)MiniJSON.Json.Deserialize(dragonBonesJSON.text), name, scale); // Unity default Scale Factor.
			name = dragonBonesJSON.name;
			int index = name.LastIndexOf("_ske");
			if(index>0)
            {
				name = name.Substring(0,index);
				data.name = name;
			}

			_dragonBonesDataMap[name] = data;
			return data;
		}

		/**
         * @language zh_CN
         * 加载、解析并添加贴图集数据。
         * @param textureAtlas 贴图集数据
         * @param name 为数据指定一个名称，以便可以通过这个名称获取数据，如果未设置，则使用数据中的名称。
         * @param scale 为贴图集设置一个缩放值。
         * @returns 贴图集数据
         * @see #ParseTextureAtlasData()
         * @see #GetTextureAtlasData()
         * @see #AddTextureAtlasData()
         * @see #RemoveTextureAtlasData()
         * @see DragonBones.UnityTextureAtlasData
         */
		protected UnityTextureAtlasData LoadTextureAtlasData(UnityDragonBonesData.TextureAtlas textureAtlas, string name , float scale = 0.0f,bool isUGUI=false)
		{
			UnityTextureAtlasData textureAtlasData = null;
			if (_pathTextureAtlasDataMap.ContainsKey(name+textureAtlas.texture.name))
			{
				textureAtlasData = _pathTextureAtlasDataMap[name+textureAtlas.texture.name] as UnityTextureAtlasData;
				#if UNITY_EDITOR
				if(!Application.isPlaying)
                {
					textureAtlasData.imagePath = AssetDatabase.GetAssetPath(textureAtlas.texture);
					textureAtlasData.imagePath =textureAtlasData.imagePath.Substring(0,textureAtlasData.imagePath.Length-4);
				}
                #endif
                _RefreshTextureAtlas(textureAtlasData,isUGUI,true);
                if (isUGUI)
                {
                    textureAtlas.uiMaterial = textureAtlasData.uiTexture;
                }
                else
                {
                    textureAtlas.material = textureAtlasData.texture;
                }
			}
			else
			{
				Dictionary<string, object> textureJSONData = (Dictionary<string, object>)MiniJSON.Json.Deserialize(textureAtlas.textureAtlasJSON.text);
				textureAtlasData = ParseTextureAtlasData(textureJSONData, null, name, scale) as UnityTextureAtlasData;
	
				if(textureJSONData.ContainsKey("width"))
                {
					textureAtlasData.width = uint.Parse(textureJSONData["width"].ToString());
				}
				if(textureJSONData.ContainsKey("height"))
                {
					textureAtlasData.height = uint.Parse(textureJSONData["height"].ToString());
				}

				if (textureAtlasData != null)
				{
					textureAtlasData.uiTexture = textureAtlas.uiMaterial;
					textureAtlasData.texture = textureAtlas.material;
					#if UNITY_EDITOR
					if(!Application.isPlaying)
                    {
						textureAtlasData.imagePath = AssetDatabase.GetAssetPath(textureAtlas.texture);
						textureAtlasData.imagePath =textureAtlasData.imagePath.Substring(0,textureAtlasData.imagePath.Length-4);
                        _RefreshTextureAtlas(textureAtlasData,isUGUI,true);
						if(isUGUI) textureAtlas.uiMaterial = textureAtlasData.uiTexture;
						else textureAtlas.material = textureAtlasData.texture;
					}
					#endif

					textureAtlasData.name = name;
					_pathTextureAtlasDataMap[name+textureAtlas.texture.name] = textureAtlasData;
				}
			}
			return textureAtlasData;
		}


        
        /**
         * @language zh_CN
         * 刷新贴图集数据中贴图。
         * @see #ParseTextureAtlasData()
         * @see #GetTextureAtlasData()
         * @see #AddTextureAtlasData()
         * @see #RemoveTextureAtlasData()
         * @see DragonBones.UnityTextureAtlasData
         */
		public void RefreshAllTextureAtlas(UnityArmatureComponent unityArmature)
        {
            foreach (var textureAtlasDatas in _textureAtlasDataMap.Values)
            {
                foreach (UnityTextureAtlasData textureAtlasData in textureAtlasDatas)
                {
                    _RefreshTextureAtlas(textureAtlasData,unityArmature.isUGUI);
                }
            }
        }

		/**
         * @language zh_CN
         * 用外部贴图替换display贴图。
         * @param dragonBonesName 指定的龙骨数据名称。
         * @param armatureName 指定的骨架名称。
         * @param slotName 指定的插槽名称。
         * @param displayName 指定的显示对象名称。
         * @param slot 指定的插槽实例。
         * @param texture 新的贴图。
         * @param material 新的材质。
         * @param isUGUI 是否为ugui。
         * @param displayIndex 要替换的显示对象的索引，如果未设置，则替换当前正在显示的显示对象。
         * @version DragonBones 4.5
         */
		public void ReplaceSlotDisplay(string dragonBonesName, string armatureName, string slotName, string displayName, Slot slot,Texture2D texture,Material material,bool isUGUI = false ,int displayIndex = -1)
		{
			var dataPackage = new BuildArmaturePackage();
			if (_FillBuildArmaturePackage(dataPackage, dragonBonesName, armatureName, null, null))
			{
                var displays = dataPackage.skin.GetDisplays(slotName);

                DisplayData prevDispalyData = null;
                foreach (var displayData in displays)
                {
                    if (displayData.name == displayName)
                    {
                        prevDispalyData = displayData;
                        break;
                    }
                }

                if (prevDispalyData == null || prevDispalyData.type != DisplayType.Image || prevDispalyData.type != DisplayType.Mesh)
                {
                    return;
                }

                TextureData prevTextureData = (prevDispalyData as ImageDisplayData).texture;
                UnityTextureData newTextureData = new UnityTextureData();
                newTextureData.CopyFrom(prevTextureData);
                newTextureData.rotated = false;
                newTextureData.region.x = 0.0f;
                newTextureData.region.y = 0.0f;
                newTextureData.region.width = texture.width;
                newTextureData.region.height = texture.height;
                newTextureData.frame = newTextureData.region;
                newTextureData.name = prevTextureData.name;
                newTextureData.parent = new UnityTextureAtlasData();
                newTextureData.parent.width = (uint)texture.width;
                newTextureData.parent.height = (uint)texture.height;
                if (isUGUI)
                {
                    (newTextureData.parent as UnityTextureAtlasData).uiTexture = material;
                }
                else
                {
                    (newTextureData.parent as UnityTextureAtlasData).texture = material;
                }

                material.mainTexture = texture;

                ImageDisplayData newDisplayData = prevDispalyData is ImageDisplayData ? new ImageDisplayData() : new MeshDisplayData();
                newDisplayData.type = prevDispalyData.type;
                newDisplayData.name = prevDispalyData.name;
                newDisplayData.path = prevDispalyData.path;
                newDisplayData.transform.CopyFrom(prevDispalyData.transform);
                newDisplayData.pivot.CopyFrom((prevDispalyData as ImageDisplayData).pivot);
                newDisplayData.texture = newTextureData;

                if (newDisplayData is MeshDisplayData)
                {
                    (newDisplayData as MeshDisplayData).inheritAnimation = (prevDispalyData as MeshDisplayData).inheritAnimation;
                    (newDisplayData as MeshDisplayData).offset = (prevDispalyData as MeshDisplayData).offset;
                    (newDisplayData as MeshDisplayData).weight = (prevDispalyData as MeshDisplayData).weight;
                }

                _ReplaceSlotDisplay(dataPackage, newDisplayData, slot, displayIndex);
                
            }
		}
    }
}