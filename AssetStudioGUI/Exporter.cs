using System.IO;
using System.Linq;
using System.Text;
using System.Windows.Forms;
using System.Collections.Generic;
using Newtonsoft.Json;
using SixLabors.ImageSharp;
using SixLabors.ImageSharp.PixelFormats;
using SixLabors.ImageSharp.Processing;
using AssetStudio;

namespace AssetStudioGUI {
	internal static class Exporter {

		public static bool g_ohms_export_with_structure = false;

		public static bool ExportTexture2D(AssetItem item, string exportPath) {
			var m_Texture2D = (Texture2D)item.Asset;
			if (Properties.Settings.Default.convertTexture) {
				var type = Properties.Settings.Default.convertType;
				if (!TryExportFile(exportPath, item, "." + type.ToString().ToLower(), out var exportFullPath))
					return false;
				var image = m_Texture2D.ConvertToImage(true);
				if (image == null)
					return false;
				using (image) {
					using (var file = File.OpenWrite(exportFullPath)) {
						image.WriteToStream(file, type);
					}
					return true;
				}
			}
			else {
				if (!TryExportFile(exportPath, item, ".tex", out var exportFullPath))
					return false;
				File.WriteAllBytes(exportFullPath, m_Texture2D.image_data.GetData());
				return true;
			}
		}

		public static bool ExportTexture2D_PNG(AssetItem item, string exportPath, out string exportFullPath) {
			var m_Texture2D = (Texture2D)item.Asset;
			if (!TryExportFile(exportPath, item, ".png", out exportFullPath))
				return false;
			var image = m_Texture2D.ConvertToImage(true);
			if (image == null)
				return false;
			using (image) {
				using (var file = File.OpenWrite(exportFullPath)) {
					image.WriteToStream(file, ImageFormat.Png);
				}
				return true;
			}
		}

		public static bool ExportTexture2D_Combine(AssetItem itemRGB, AssetItem itemA, string exportPath) {
			try {
				var m_Texture2D_RGB = (Texture2D)itemRGB.Asset;
				var m_Texture2D_A = (Texture2D)itemA.Asset;

				ImageFormat type;
				type = Properties.Settings.Default.convertTexture ?
					Properties.Settings.Default.convertType : ImageFormat.Png;

				if (!TryExportFile(exportPath, itemRGB, "." + type.ToString().ToLower(), out var exportFullPath)) {
					throw new System.Exception("0");
				}

				var image0 = m_Texture2D_RGB.ConvertToImage(true);
				var image1 = m_Texture2D_A.ConvertToImage(true);

				if (image0 == null || image1 == null) {
					throw new System.Exception("1");
				}

				if (image1.Size != image0.Size) {
					var sampler = new SixLabors.ImageSharp.Processing.Processors.Transforms.BicubicResampler();
					image1.Mutate(a => a.Resize(image0.Size, sampler, false));
				}

				for (int i = 0; i < image0.Width; i++) {
					for (int j = 0; j < image0.Height; j++) {
						var oc = image0[i, j];
						image0[i, j] = new Bgra32(oc.R, oc.G, oc.B, image1[i, j].R);
					}
				}
				using (image0) {
					using (var file = File.OpenWrite(exportFullPath)) {
						image0.WriteToStream(file, type);
					}
				}
			}
			catch (System.Exception e) {
				var res0 = ExportTexture2D(itemRGB, exportPath);
				var res1 = ExportTexture2D(itemA, exportPath);
				return res0 || res1;
			}

			var np = Path.Combine(exportPath, "original");

			ExportTexture2D(itemRGB, np);
			ExportTexture2D(itemA, np);

			return true;
		}

		public static bool ExportAudioClip(AssetItem item, string exportPath) {
			var m_AudioClip = (AudioClip)item.Asset;
			var m_AudioData = m_AudioClip.m_AudioData.GetData();
			if (m_AudioData == null || m_AudioData.Length == 0)
				return false;
			var converter = new AudioClipConverter(m_AudioClip);
			if (Properties.Settings.Default.convertAudio && converter.IsSupport) {
				if (!TryExportFile(exportPath, item, ".wav", out var exportFullPath))
					return false;
				var buffer = converter.ConvertToWav();
				if (buffer == null)
					return false;
				File.WriteAllBytes(exportFullPath, buffer);
			}
			else {
				if (!TryExportFile(exportPath, item, converter.GetExtensionName(), out var exportFullPath))
					return false;
				File.WriteAllBytes(exportFullPath, m_AudioData);
			}
			return true;
		}

		public static bool ExportShader(AssetItem item, string exportPath) {
			if (!TryExportFile(exportPath, item, ".shader", out var exportFullPath))
				return false;
			var m_Shader = (Shader)item.Asset;
			var str = m_Shader.Convert();
			File.WriteAllText(exportFullPath, str);
			return true;
		}

		public static bool ExportTextAsset(AssetItem item, string exportPath) {
			var m_TextAsset = (TextAsset)(item.Asset);
			var extension = ".txt";
			if (Properties.Settings.Default.restoreExtensionName) {
				if (!string.IsNullOrEmpty(item.Container)) {
					extension = Path.GetExtension(item.Container);
				}
			}
			if (!TryExportFile(exportPath, item, extension, out var exportFullPath))
				return false;
			File.WriteAllBytes(exportFullPath, m_TextAsset.m_Script);
			return true;
		}

		public static bool ExportTextAsset_NoAppendingExtension(AssetItem item, string exportPath) {
			var m_TextAsset = (TextAsset)(item.Asset);
			if (!TryExportFile(exportPath, item, "", out var exportFullPath))
				return false;
			File.WriteAllBytes(exportFullPath, m_TextAsset.m_Script);
			return true;
		}

		public static bool ExportMonoBehaviour(AssetItem item, string exportPath) {
			if (!TryExportFile(exportPath, item, ".json", out var exportFullPath))
				return false;
			var m_MonoBehaviour = (MonoBehaviour)item.Asset;
			var type = m_MonoBehaviour.ToType();
			if (type == null) {
				var m_Type = StudioCore.m_studio.MonoBehaviourToTypeTree(m_MonoBehaviour);
				type = m_MonoBehaviour.ToType(m_Type);
			}
			var str = JsonConvert.SerializeObject(type, Formatting.Indented);
			File.WriteAllText(exportFullPath, str);
			return true;
		}

		public static bool ExportFont(AssetItem item, string exportPath) {
			var m_Font = (Font)item.Asset;
			if (m_Font.m_FontData != null) {
				var extension = ".ttf";
				if (m_Font.m_FontData[0] == 79 && m_Font.m_FontData[1] == 84 && m_Font.m_FontData[2] == 84 && m_Font.m_FontData[3] == 79) {
					extension = ".otf";
				}
				if (!TryExportFile(exportPath, item, extension, out var exportFullPath))
					return false;
				File.WriteAllBytes(exportFullPath, m_Font.m_FontData);
				return true;
			}
			return false;
		}

		public static bool ExportMesh(AssetItem item, string exportPath) {
			var m_Mesh = (Mesh)item.Asset;
			if (m_Mesh.m_VertexCount <= 0)
				return false;
			if (!TryExportFile(exportPath, item, ".obj", out var exportFullPath))
				return false;
			var sb = new StringBuilder();

			sb.AppendLine($"# AssetStudio v0.16.47-OHMS-v{Application.ProductVersion}");
			sb.AppendLine("g " + m_Mesh.m_Name);

			#region Vertices
			if (m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0) {
				return false;
			}
			int c = 3;
			if (m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4) {
				c = 4;
			}
			sb.AppendFormat("# Vertex Count: {0}\r\n", m_Mesh.m_VertexCount);
			for (int v = 0; v < m_Mesh.m_VertexCount; v++) {
				sb.AppendFormat("v {0} {1} {2}\r\n", -m_Mesh.m_Vertices[v * c], m_Mesh.m_Vertices[v * c + 1], m_Mesh.m_Vertices[v * c + 2]);
			}
			#endregion

			#region UV0
			if (m_Mesh.m_UV0?.Length > 0) {
				c = 4;
				if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 2) {
					c = 2;
				}
				else if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 3) {
					c = 3;
				}
				sb.AppendFormat("# UV0 count of a vertex: {0}\r\n", c);
				for (int v = 0; v < m_Mesh.m_VertexCount; v++) {
					sb.AppendFormat("vt {0} {1}\r\n", m_Mesh.m_UV0[v * c], m_Mesh.m_UV0[v * c + 1]);
				}
			}
			#endregion

			#region UV1
			if (m_Mesh.m_UV1?.Length > 0) {
				c = 4;
				if (m_Mesh.m_UV1.Length == m_Mesh.m_VertexCount * 2) {
					c = 2;
				}
				else if (m_Mesh.m_UV1.Length == m_Mesh.m_VertexCount * 3) {
					c = 3;
				}
				sb.AppendFormat("# UV1 count of a vertex: {0}\r\n", c);
				for (int v = 0; v < m_Mesh.m_VertexCount; v++) {
					sb.AppendFormat("vt1 {0} {1}\r\n", m_Mesh.m_UV1[v * c], m_Mesh.m_UV1[v * c + 1]);
				}
			}
			#endregion

			#region Normals
			if (m_Mesh.m_Normals?.Length > 0) {
				if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3) {
					c = 3;
				}
				else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4) {
					c = 4;
				}
				for (int v = 0; v < m_Mesh.m_VertexCount; v++) {
					sb.AppendFormat("vn {0} {1} {2}\r\n", -m_Mesh.m_Normals[v * c], m_Mesh.m_Normals[v * c + 1], m_Mesh.m_Normals[v * c + 2]);
				}
			}
			#endregion

			#region Face
			int sum = 0;
			for (var i = 0; i < m_Mesh.m_SubMeshes.Length; i++) {
				sb.AppendLine($"g {m_Mesh.m_Name}_{i}");
				int indexCount = (int)m_Mesh.m_SubMeshes[i].indexCount;
				var end = sum + indexCount / 3;
				for (int f = sum; f < end; f++) {
					sb.AppendFormat("f {0}/{0}/{0} {1}/{1}/{1} {2}/{2}/{2}\r\n", m_Mesh.m_Indices[f * 3 + 2] + 1, m_Mesh.m_Indices[f * 3 + 1] + 1, m_Mesh.m_Indices[f * 3] + 1);
				}
				sum = end;
			}
			#endregion

			sb.Replace("NaN", "0");
			File.WriteAllText(exportFullPath, sb.ToString());
			return true;
		}

		public static bool ExportMeshOHMS(AssetItem item, string exportPath) {
			var m_Mesh = (Mesh)item.Asset;
			if (m_Mesh.m_VertexCount <= 0)
				return false;
			if (!TryExportFile(exportPath, item, ".ttobj", out var exportFullPath))
				return false;
			var sb = new StringBuilder();

			if (m_Mesh.m_Vertices == null || m_Mesh.m_Vertices.Length == 0) {
				return false;
			}

			sb.AppendLine($"# AssetStudio v0.16.47-OHMS-v{Application.ProductVersion}");
			sb.AppendLine($"# Name of Mesh: \"{m_Mesh.m_Name}\"");
			sb.AppendLine("");

			#region Vertices
			int c_V;
			if (m_Mesh.m_Vertices.Length == m_Mesh.m_VertexCount * 4) {
				c_V = 4;
			}
			else {
				c_V = 3;
			}

			int c_UV0 = 0;
			if (m_Mesh.m_UV0?.Length > 0) {
				if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 2) {
					c_UV0 = 2;
				}
				else if (m_Mesh.m_UV0.Length == m_Mesh.m_VertexCount * 3) {
					c_UV0 = 3;
				}
				else {
					c_UV0 = 4;
				}
			}

			int c_UV1 = 0;
			if (m_Mesh.m_UV1?.Length > 0) {
				if (m_Mesh.m_UV1.Length == m_Mesh.m_VertexCount * 2) {
					c_UV1 = 2;
				}
				else if (m_Mesh.m_UV1.Length == m_Mesh.m_VertexCount * 3) {
					c_UV1 = 3;
				}
				else {
					c_UV1 = 4;
				}
			}

			int c_N = 0;
			if (m_Mesh.m_Normals?.Length > 0) {
				if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 3) {
					c_N = 3;
				}
				else if (m_Mesh.m_Normals.Length == m_Mesh.m_VertexCount * 4) {
					c_N = 4;
				}
			}

			sb.Append($"# Vertex Count: {m_Mesh.m_VertexCount}\r\n");
			sb.Append($"# Pos count of a vertex: {c_V}\r\n");
			sb.Append($"# UV0 count of a vertex: {c_UV0}\r\n");
			sb.Append($"# UV1 count of a vertex: {c_UV1}\r\n");
			sb.Append($"# Nor count of a vertex: {c_N}\r\n\r\n");

			sb.AppendLine("g Root\r\n");

			for (int v = 0; v < m_Mesh.m_VertexCount; v++) {
				sb.Append($"v ");

				switch (c_V) {
				case 3:
					sb.AppendFormat("{0} {1} {2}", m_Mesh.m_Vertices[v * c_V], m_Mesh.m_Vertices[v * c_V + 1], -m_Mesh.m_Vertices[v * c_V + 2]);
					break;
				case 4:
					sb.AppendFormat("{0} {1} {2} {3}", m_Mesh.m_Vertices[v * c_V], m_Mesh.m_Vertices[v * c_V + 1], -m_Mesh.m_Vertices[v * c_V + 2], m_Mesh.m_Vertices[v * c_V + 3]);
					break;
				}

				for (int i = 0; i < c_UV0; ++i) {
					sb.Append($" {m_Mesh.m_UV0[v * c_UV0 + i]}");
				}
				for (int i = 0; i < c_UV1; ++i) {
					sb.Append($" {m_Mesh.m_UV1[v * c_UV1 + i]}");
				}

				switch (c_N) {
				case 3:
					sb.AppendFormat(" {0} {1} {2}", m_Mesh.m_Normals[v * c_N], m_Mesh.m_Normals[v * c_N + 1], -m_Mesh.m_Normals[v * c_N + 2]);
					break;
				case 4:
					sb.AppendFormat(" {0} {1} {2} {3}", m_Mesh.m_Normals[v * c_N], m_Mesh.m_Normals[v * c_N + 1], -m_Mesh.m_Normals[v * c_N + 2], m_Mesh.m_Normals[v * c_N + 3]);
					break;
				}
				sb.Append("\r\n");
			}
			#endregion

			#region Face
			int sum = 0;
			for (var i = 0; i < m_Mesh.m_SubMeshes.Length; i++) {
				sb.Append($"\r\ng Sub {i}\r\n\r\n");
				int indexCount = (int)m_Mesh.m_SubMeshes[i].indexCount;
				var end = sum + indexCount / 3;
				for (int f = sum; f < end; f++) {
					sb.AppendFormat("f {2} {1} {0}\r\n", m_Mesh.m_Indices[f * 3], m_Mesh.m_Indices[f * 3 + 1], m_Mesh.m_Indices[f * 3 + 2]);
				}
				sum = end;
			}
			sb.AppendLine($"\r\n# Face count: {sum}");
			#endregion

			sb.Replace("NaN", "0");
			File.WriteAllText(exportFullPath, sb.ToString());
			return true;
		}

		public static bool ExportVideoClip(AssetItem item, string exportPath) {
			var m_VideoClip = (VideoClip)item.Asset;
			if (m_VideoClip.m_ExternalResources.m_Size > 0) {
				if (!TryExportFile(exportPath, item, Path.GetExtension(m_VideoClip.m_OriginalPath), out var exportFullPath))
					return false;
				m_VideoClip.m_VideoData.WriteData(exportFullPath);
				return true;
			}
			return false;
		}

		public static bool ExportMovieTexture(AssetItem item, string exportPath) {
			var m_MovieTexture = (MovieTexture)item.Asset;
			if (!TryExportFile(exportPath, item, ".ogv", out var exportFullPath))
				return false;
			File.WriteAllBytes(exportFullPath, m_MovieTexture.m_MovieData);
			return true;
		}

		public static bool ExportSprite(AssetItem item, string exportPath) {
			var type = Properties.Settings.Default.convertType;
			if (!TryExportFile(exportPath, item, "." + type.ToString().ToLower(), out var exportFullPath))
				return false;
			var image = ((Sprite)item.Asset).GetImage();
			if (image != null) {
				using (image) {
					using (var file = File.OpenWrite(exportFullPath)) {
						image.WriteToStream(file, type);
					}
					return true;
				}
			}
			return false;
		}

		public static bool ExportTexture2D_CombineRGBA(AssetItem itemRGB, AssetItem itemA, string exportPath) {
			var m_Texture2DRGB = (Texture2D)itemRGB.Asset;
			var m_Texture2DA = (Texture2D)itemA.Asset;
			if (Properties.Settings.Default.convertTexture) {
				var type = Properties.Settings.Default.convertType;
				if (!TryExportFile(exportPath, itemRGB, "." + type.ToString().ToLower(), out var exportFullPath))
					return false;
				var imageRGB = m_Texture2DRGB.ConvertToImage(true);
				if (imageRGB == null)
					return false;
				var imageA = m_Texture2DA.ConvertToImage(true);
				if (imageA != null) {
					imageRGB = SpriteHelper.CombineChannelA(imageRGB, imageA);
				}
				using (imageRGB) {
					using var file = File.OpenWrite(exportFullPath);
					imageRGB.WriteToStream(file, type);
				}
				return true;
			}
			else {
				if (!TryExportFile(exportPath, itemRGB, ".tex", out var exportFullPath))
					return false;
				File.WriteAllBytes(exportFullPath, m_Texture2DRGB.image_data.GetData());
				if (!TryExportFile(exportPath, itemA, ".tex", out var exportFullPath2))
					return false;
				File.WriteAllBytes(exportFullPath2, m_Texture2DA.image_data.GetData());
				return true;
			}
		}

		public static bool ExportRawFile(AssetItem item, string exportPath) {
			if (!TryExportFile(exportPath, item, ".dat", out var exportFullPath))
				return false;
			File.WriteAllBytes(exportFullPath, item.Asset.GetRawData());
			return true;
		}

		public static bool ExportAnimatorConvert(AssetItem item, string exportPath, List<AssetItem> animationList = null) {
			if (!TryExportFile(exportPath, item, ".fbx", out var exportFullPath))
				return false;
			var m_Animator = (Animator)item.Asset;
			var convert = animationList != null
				? new ModelConverter(m_Animator, Properties.Settings.Default.convertType, animationList.Select(x => (AnimationClip)x.Asset).ToArray())
				: new ModelConverter(m_Animator, Properties.Settings.Default.convertType);
			ExportFbx(convert, exportFullPath);
			return true;
		}

		private static bool TryExportFile(string dir, AssetItem item, string extension, out string fullPath) {
			if (g_ohms_export_with_structure) {
				fullPath = Path.Combine(dir, item.ID + ".ttbin");
				if (!File.Exists(fullPath)) {
					Directory.CreateDirectory(dir);
					return true;
				}
			}
			else {
				var fileName = FixFileName(item.Text);
				fullPath = Path.Combine(dir, fileName + extension);
				if (!File.Exists(fullPath)) {
					Directory.CreateDirectory(dir);
					return true;
				}
				fullPath = Path.Combine(dir, fileName + item.UniqueID + extension);
				if (!File.Exists(fullPath)) {
					Directory.CreateDirectory(dir);
					return true;
				}
			}
			return false;
		}

		public static bool ExportAnimator(AssetItem item, string exportPath, List<AssetItem> animationList = null) {
			var exportFullPath = Path.Combine(exportPath, item.Text, item.Text + ".fbx");
			if (File.Exists(exportFullPath)) {
				exportFullPath = Path.Combine(exportPath, item.Text + item.UniqueID, item.Text + ".fbx");
			}
			var m_Animator = (Animator)item.Asset;
			var convert = animationList != null
				? new ModelConverter(m_Animator, Properties.Settings.Default.convertType, animationList.Select(x => (AnimationClip)x.Asset).ToArray())
				: new ModelConverter(m_Animator, Properties.Settings.Default.convertType);
			ExportFbx(convert, exportFullPath);
			return true;
		}

		public static void ExportGameObject(GameObject gameObject, string exportPath, List<AssetItem> animationList = null) {
			var convert = animationList != null
				? new ModelConverter(gameObject, Properties.Settings.Default.convertType, animationList.Select(x => (AnimationClip)x.Asset).ToArray())
				: new ModelConverter(gameObject, Properties.Settings.Default.convertType);
			exportPath = exportPath + FixFileName(gameObject.m_Name) + ".fbx";
			ExportFbx(convert, exportPath);
		}

		public static void ExportGameObjectMerge(List<GameObject> gameObject, string exportPath, List<AssetItem> animationList = null) {
			var rootName = Path.GetFileNameWithoutExtension(exportPath);
			var convert = animationList != null
				? new ModelConverter(rootName, gameObject, Properties.Settings.Default.convertType, animationList.Select(x => (AnimationClip)x.Asset).ToArray())
				: new ModelConverter(rootName, gameObject, Properties.Settings.Default.convertType);
			ExportFbx(convert, exportPath);
		}

		private static void ExportFbx(IImported convert, string exportPath) {
			var eulerFilter = Properties.Settings.Default.eulerFilter;
			var filterPrecision = (float)Properties.Settings.Default.filterPrecision;
			var exportAllNodes = Properties.Settings.Default.exportAllNodes;
			var exportSkins = Properties.Settings.Default.exportSkins;
			var exportAnimations = Properties.Settings.Default.exportAnimations;
			var exportBlendShape = Properties.Settings.Default.exportBlendShape;
			var castToBone = Properties.Settings.Default.castToBone;
			var boneSize = (int)Properties.Settings.Default.boneSize;
			var exportAllUvsAsDiffuseMaps = Properties.Settings.Default.exportAllUvsAsDiffuseMaps;
			var scaleFactor = (float)Properties.Settings.Default.scaleFactor;
			var fbxVersion = Properties.Settings.Default.fbxVersion;
			var fbxFormat = Properties.Settings.Default.fbxFormat;
			ModelExporter.ExportFbx(exportPath, convert, eulerFilter, filterPrecision,
				exportAllNodes, exportSkins, exportAnimations, exportBlendShape, castToBone, boneSize, exportAllUvsAsDiffuseMaps, scaleFactor, fbxVersion, fbxFormat == 1);
		}

		public static bool ExportDumpFile(AssetItem item, string exportPath) {
			if (!TryExportFile(exportPath, item, ".txt", out var exportFullPath))
				return false;
			var str = item.Asset.Dump();
			if (str == null && item.Asset is MonoBehaviour m_MonoBehaviour) {
				var m_Type = StudioCore.m_studio.MonoBehaviourToTypeTree(m_MonoBehaviour);
				str = m_MonoBehaviour.Dump(m_Type);
			}
			if (str != null) {
				File.WriteAllText(exportFullPath, str);
				return true;
			}
			return false;
		}

		public static bool ExportDumpFileJson(AssetItem item, string exportPath) {
			if (!TryExportFile(exportPath, item, ".json", out var exportFullPath))
				return false;
			var type = item.Asset.ToType();
			if (type == null && item.Asset is MonoBehaviour m_MonoBehaviour) {
				var m_Type = StudioCore.m_studio.MonoBehaviourToTypeTree(m_MonoBehaviour);
				type = m_MonoBehaviour.ToType(m_Type);
			}
			var str = JsonConvert.SerializeObject(type, Formatting.Indented);
			if (str != null) {
				File.WriteAllText(exportFullPath, str);
				return true;
			}
			return false;
		}

		public static bool ExportConvertFile(AssetItem item, string exportPath) {
			switch (item.Type) {
			case ClassIDType.Texture2D:
				return ExportTexture2D(item, exportPath);
			case ClassIDType.AudioClip:
				return ExportAudioClip(item, exportPath);
			case ClassIDType.Shader:
				return ExportShader(item, exportPath);
			case ClassIDType.TextAsset:
				return ExportTextAsset(item, exportPath);
			case ClassIDType.MonoBehaviour:
				return ExportMonoBehaviour(item, exportPath);
			case ClassIDType.Font:
				return ExportFont(item, exportPath);
			case ClassIDType.Mesh:
				return ExportMesh(item, exportPath);
			case ClassIDType.VideoClip:
				return ExportVideoClip(item, exportPath);
			case ClassIDType.MovieTexture:
				return ExportMovieTexture(item, exportPath);
			case ClassIDType.Sprite:
				return ExportSprite(item, exportPath);
			case ClassIDType.Animator:
				return ExportAnimatorConvert(item, exportPath);
			case ClassIDType.AnimationClip:
				return false;
			default:
				return ExportRawFile(item, exportPath);
			}
		}

		public static bool ExportConvertFileOHMS(AssetItem item, string exportPath) {
			switch (item.Type) {
			case ClassIDType.Texture2D:
				return ExportTexture2D(item, exportPath);
			case ClassIDType.AudioClip:
				return ExportAudioClip(item, exportPath);
			case ClassIDType.Shader:
				return ExportShader(item, exportPath);
			case ClassIDType.TextAsset:
				return ExportTextAsset(item, exportPath);
			case ClassIDType.MonoBehaviour:
				return ExportMonoBehaviour(item, exportPath);
			case ClassIDType.Font:
				return ExportFont(item, exportPath);
			case ClassIDType.Mesh:
				return ExportMeshOHMS(item, exportPath);
			case ClassIDType.VideoClip:
				return ExportVideoClip(item, exportPath);
			case ClassIDType.MovieTexture:
				return ExportMovieTexture(item, exportPath);
			case ClassIDType.Sprite:
				return ExportDumpFileJson(item, exportPath);
			case ClassIDType.Animator:
				return false;
			case ClassIDType.AnimationClip:
				return false;
			case ClassIDType.AssetBundle:
				return false;
			default:
				return ExportDumpFileJson(item, exportPath);
			}
		}

		public static string FixFileName(string str) {
			if (str.Length >= 260)
				return Path.GetRandomFileName();
			return Path.GetInvalidFileNameChars().Aggregate(str, (current, c) => current.Replace(c, '_'));
		}

	}
}
