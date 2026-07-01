using AssetStudio;
using System;
using System.Collections;
using System.Collections.Generic;
using System.Collections.Specialized;
using System.IO;
using System.Linq;
using System.Security.Policy;
using System.Text;
using System.Threading.Tasks;
using static AssetStudioGUI.Exporter;
using static System.Net.Mime.MediaTypeNames;

namespace AssetStudioGUI {
	internal class ExporterArknightsCharArt(List<AssetItem> allAssets) {

		//private readonly List<AssetItem> m_allItems = allAssets;
		private readonly List<AssetItem> m_textItems = allAssets.FindAll(x => x.Type == ClassIDType.TextAsset);
		private readonly List<AssetItem> m_gameObjectItems = allAssets.FindAll(x => x.Type == ClassIDType.GameObject);
		private readonly List<AssetItem> m_monoItems = allAssets.FindAll(x => x.Type == ClassIDType.MonoBehaviour);
		private readonly List<AssetItem> m_texItems = allAssets.FindAll(x => x.Type == ClassIDType.Texture2D);
		private readonly List<AssetItem> m_matItems = allAssets.FindAll(x => x.Type == ClassIDType.Material);
		private readonly List<AssetItem> m_spItems = allAssets.FindAll(x => x.Type == ClassIDType.Sprite);

		public bool ExportAllSpineAnimations(string savePath) {
			var SpineObjects = m_gameObjectItems.FindAll(x => x.Text == "Spine");
			foreach (var SpineObject in SpineObjects) {
				var spineAsset = (GameObject)SpineObject.Asset;
				foreach (var pcomponent in spineAsset.m_Components) {
					if (!pcomponent.TryGet(out MonoBehaviour skel)) {
						continue;
					}
					string name = "";
					if (SpineObject.Container.Contains("/battle/")) {
						name = "Battle";
					}
					else if (SpineObject.Container.Contains("/building/")) {
						name = "Building";
					}
					Export_SkeletonAnimation(Path.Combine(savePath, name), skel);
				}
			}

			var FrontObjects = m_gameObjectItems.FindAll(x => x.Text == "Front");
			foreach (var SpineObject in FrontObjects) {
				var spineAsset = (GameObject)SpineObject.Asset;
				foreach (var pcomponent in spineAsset.m_Components) {
					if (!pcomponent.TryGet(out MonoBehaviour skel)) {
						continue;
					}
					Export_SkeletonAnimation(Path.Combine(savePath, "Battle-Front"), skel);
				}
			}

			var BackObjects = m_gameObjectItems.FindAll(x => x.Text == "Back");
			foreach (var SpineObject in BackObjects) {
				var spineAsset = (GameObject)SpineObject.Asset;
				foreach (var pcomponent in spineAsset.m_Components) {
					if (!pcomponent.TryGet(out MonoBehaviour skel)) {
						continue;
					}
					Export_SkeletonAnimation(Path.Combine(savePath, "Battle-Back"), skel);
				}
			}
			return true;
		}

		public bool ExportAllIllustrations(string savePath) {
			savePath = Path.Combine(savePath, "Illust");
			var IllustObjects = m_gameObjectItems.FindAll(x => x.Text.StartsWith("illust_"));

			int i = 0;
			int n = IllustObjects.Count;
			foreach (var obj in IllustObjects) {
				var asset = (GameObject)obj.Asset;
				foreach (var com in asset.m_Components) {
					if (!com.TryGet(out MonoBehaviour illust)) {
						continue;
					}
					Export_Illust(savePath, illust);
				}
				Progress.Report(++i + n, n + n);
			}
			return true;
		}

		private bool Export_SkeletonData(string savePath, MonoBehaviour skeletonDataAsset) {
			var skeletonData = skeletonDataAsset.ToType();

			if (skeletonData["skeletonJSON"] is OrderedDictionary pSkelJson) {
				if (pSkelJson["m_PathID"] is long idSkelJson) {
					var lSkelJsons = m_textItems.FindAll(x => x.m_PathID == idSkelJson);
					if (lSkelJsons.Count == 1) {
						ExportSpineTextAsset(savePath, lSkelJsons[0]);
					}
				}
			}

			if (skeletonData["atlasAssets"] is List<object> vAtlasAssets) {
				foreach (var obj in vAtlasAssets) {
					if (obj is not OrderedDictionary pAtlasAsset || pAtlasAsset["m_PathID"] is not long idAtlasAsset) {
						continue;
					}
					var lAtlasAssets = m_monoItems.FindAll(x => x.m_PathID == idAtlasAsset);
					if (lAtlasAssets.Count == 1) {
						Export_AtlasAsset(savePath, lAtlasAssets[0].Asset as MonoBehaviour);
					}
				}
			}
			return true;
		}

		private void Export_AtlasAsset(string savePath, MonoBehaviour atlasAsset) {
			var atlas = atlasAsset.ToType();

			if (atlas["atlasFile"] is OrderedDictionary pAtlas && pAtlas["m_PathID"] is long idAtlas) {
				var atlasFiles = m_textItems.FindAll(x => x.m_PathID == idAtlas);
				if (atlasFiles.Count == 1) {
					ExportSpineTextAsset(savePath, atlasFiles[0]);
				}
				else {
					;
				}
			}

			if (atlas["materials"] is List<object> vMaterials) {
				foreach (var obj in vMaterials) {
					if (obj is not OrderedDictionary pMat || pMat["m_PathID"] is not long idMat) {
						continue;
					}
					GetTexturesFromMaterialId(idMat, out long mainTexID, out long alphaTexID);
					ExportTextures(savePath, mainTexID, alphaTexID);
				}
			}

			return;
		}

		private bool Export_SkeletonAnimation(string savePath, MonoBehaviour SkeletonAnimationAsset) {
			if (SkeletonAnimationAsset.ToType()["skeletonDataAsset"] is OrderedDictionary pSkelDataAsset) {
				if (pSkelDataAsset["m_PathID"] is long idSkelDataAsset) {
					var lSkelDataAsset = m_monoItems.FindAll(x => x.m_PathID == idSkelDataAsset);
					if (lSkelDataAsset.Count == 1) {
						return Export_SkeletonData(savePath, lSkelDataAsset[0].Asset as MonoBehaviour);
					}
					else {
						;
					}
				}
			}
			return false;
		}

		private bool Export_Illust(string savePath, MonoBehaviour illust) {
			long material = 0;
			long sprite = 0;
			{
				var o = illust.ToType();
				if (o["m_Material"] is OrderedDictionary pMat) {
					material = pMat["m_PathID"] as long? ?? 0;
				}
				if (o["m_Sprite"] is OrderedDictionary pSp) {
					sprite = pSp["m_PathID"] as long? ?? 0;
				}
			}

			GetTexturesFromMaterialId(material, out var mainTex, out var alphaTex);
			GetTexturesFromSpriteId(sprite, out var mainTex1, out var alphaTex1);

			if (mainTex != mainTex1) {
				if (mainTex == 0) {
					mainTex = mainTex1;
				}
				else if (mainTex1 != 0) {
					;
				}
			}
			if (alphaTex != alphaTex1) {
				if (alphaTex == 0) {
					alphaTex = alphaTex1;
				}
				else if (alphaTex1 != 0) {
					;
				}
			}

			ExportTextures(savePath, mainTex, alphaTex);
			return true;
		}

		private void GetTexturesFromMaterialId(long matid, out long idMainTex, out long idAlphaTex) {
			idMainTex = 0;
			idAlphaTex = 0;
			if (matid == 0) {
				return;
			}

			var lMats = m_matItems.FindAll(x => x.m_PathID == matid);
			if (lMats.Count != 1) {
				return;
			}
			var asset = lMats[0].Asset as Material;

			foreach (var pair in asset.m_SavedProperties.m_TexEnvs) {
				switch (pair.Key) {
				case "_AlphaTex":
					if (idAlphaTex == 0) {
						idAlphaTex = pair.Value.m_Texture.m_PathID;
					}
					break;
				case "_MainTex":
					if (idMainTex == 0) {
						idMainTex = pair.Value.m_Texture.m_PathID;
					}
					break;
				}
			}
			return;
		}

		private void GetTexturesFromSpriteId(long spid, out long idMainTex, out long idAlphaTex) {
			idMainTex = 0;
			idAlphaTex = 0;
			if (spid == 0) {
				return;
			}

			var lSp = m_spItems.FindAll(x => x.m_PathID == spid);
			if (lSp.Count != 1) {
				return;
			}
			var sp = lSp[0].Asset as Sprite;

			idMainTex = sp.m_RD.texture.m_PathID;
			idAlphaTex = sp.m_RD.alphaTexture.m_PathID;
			return;
		}

		private bool ExportTextures(string savePath, long mainTexID, long alphaTexID) {
			bool res0 = false, res1 = false;

			if (mainTexID != 0 && alphaTexID != 0) {
				var mainTex = m_texItems.Find(x => x.m_PathID == mainTexID);
				var alphaTex = m_texItems.Find(x => x.m_PathID == alphaTexID);
				if (ExportTexture2D_CombineRGBA(mainTex, alphaTex, savePath))
					return true;
			}

			if (mainTexID != 0) {
				var mainTexs = m_texItems.FindAll(x => x.m_PathID == mainTexID);
				foreach (var tex in mainTexs) {
					ExportTexture2D(tex, savePath);
					res0 = true;
				}
			}
			if (alphaTexID != 0) {
				var alphaTexs = m_texItems.FindAll(x => x.m_PathID == alphaTexID);
				foreach (var tex in alphaTexs) {
					ExportTexture2D(tex, savePath);
					res1 = true;
				}
			}
			return res0 || res1;
		}

		private static bool ExportTextures(string savePath, AssetItem mainTex, AssetItem alphaTex) {
			bool res0 = false, res1 = false;
			if (mainTex != null)
				res0 = ExportTexture2D_PNG(mainTex, savePath, out var _);
			if (alphaTex != null)
				res1 = ExportTexture2D_PNG(alphaTex, savePath, out var _);
			return res0 || res1;
		}

		private static bool ExportSpineTextAsset(string savePath, AssetItem item) {
			return ExportTextAsset_NoAppendingExtension(item, savePath);
		}

	}
}
