using Engine;
using Engine.Graphics;

namespace Game
{
	/// <summary>
	/// Varilla del granjero: sirve para marcar 2 puntos que definen el área
	/// de cultivo donde trabajará la criatura granjera.
	/// Visualmente es igual al Rod pero con un tinte dorado/ámbar.
	/// </summary>
	public class FarmerWandBlock : Block
	{
		// Index deseado (BlocksManager.AllocateBlock lo sobreescribe por reflexión
		// si el bloque se registra dinámicamente).
		public static int Index = 670;

		public BlockMesh m_standaloneBlockMesh = new BlockMesh();

		// Color dorado de la varilla del granjero.
		public static readonly Color WandColor = new Color(255, 190, 45, 255);

		public override void Initialize()
		{
			Model model = ContentManager.Get<Model>("Models/Rod");
			Matrix boneAbsoluteTransform = BlockMesh.GetBoneAbsoluteTransform(
				model.FindMesh("IronRod", true).ParentBone);

			this.m_standaloneBlockMesh.AppendModelMeshPart(
				model.FindMesh("IronRod", true).MeshParts[0],
				boneAbsoluteTransform * Matrix.CreateTranslation(0f, -0.5f, 0f),
				false, false, false, false, Color.White);

			base.Initialize();
		}

		// La varilla no genera geometría en el terreno (es solo un item/objeto).
		public override void GenerateTerrainVertices(
			BlockGeometryGenerator generator, TerrainGeometry geometry,
			int value, int x, int y, int z)
		{
		}

		public override void DrawBlock(
			PrimitivesRenderer3D primitivesRenderer, int value, Color color,
			float size, ref Matrix matrix, DrawBlockEnvironmentData environmentData)
		{
			// Forzamos el color dorado de la varilla, ignorando el color entrante.
			BlocksManager.DrawMeshBlock(
				primitivesRenderer, this.m_standaloneBlockMesh,
				WandColor, 2f * size, ref matrix, environmentData);
		}

		// El bloque se puede usar (click derecho) y con ello dispara OnUse de su behavior.
		public override bool IsAimable_(int value) => false;

		// Necesitamos que el bloque interactúe, pero no que sea "placeable".
		public override bool IsPlaceable_(int value) => false;

		// Bloque con comportamiento personalizado → editable/interactivo a nivel de item.
		public override bool IsInteractive(SubsystemTerrain subsystemTerrain, int value) => true;
		public override bool IsEditable_(int value) => true;
	}
}
