using AssetStudio;

namespace AssetStudioShell {
	public class Manager {
		private AssetsManager m_assetsManager = new();
		public AssetsManager AssetManager {
			get {
				return m_assetsManager;
			}
		}
		private AssemblyLoader m_assemblyLoader = new AssemblyLoader();

		public void Clear() {
			m_assetsManager.Clear();
			m_assemblyLoader.Clear();
		}

	}
}
