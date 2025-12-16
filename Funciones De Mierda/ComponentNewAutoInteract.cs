using Engine;
using GameEntitySystem;
using System.Globalization;
using TemplatesDatabase;

namespace Game
{
	public class ComponentNewAutoInteract : Component, IUpdateable
	{
		public SubsystemTerrain m_subsystemTerrain;
		public SubsystemBodies m_subsystemBodies;
		public SubsystemMovingBlocks m_subsystemMovingBlocks;
		public SubsystemGameInfo m_subsystemGameInfo;
		public SubsystemTime m_subsystemTime;
		public SubsystemAudio m_subsystemAudio;
		public SubsystemSoundMaterials m_subsystemSoundMaterials;
		public SubsystemBlockBehaviors m_subsystemBlockBehaviors;
		public ComponentHealth m_componentHealth;
		public ComponentMiner m_componentMiner;
		public Random m_random = new Game.Random();
		public static Random s_random = new Game.Random();

		public ComponentCreature ComponentCreature { get; set; }
		public float AutoInteractRate { get; set; }
		public UpdateOrder UpdateOrder => UpdateOrder.Default;

		public void Update(float dt)
		{
			if ((m_componentHealth != null && !(m_componentHealth.Health > 0f)) || !(AutoInteractRate > 0f) || !m_random.Bool(AutoInteractRate) || !m_subsystemTime.PeriodicGameTimeEvent(1.0, (float)(GetHashCode() % 100) / 100f))
			{
				return;
			}
			ComponentCreatureModel componentCreatureModel = ComponentCreature.ComponentCreatureModel;
			Vector3 eyePosition = componentCreatureModel.EyePosition;
			Vector3 forwardVector = componentCreatureModel.EyeRotation.GetForwardVector();
			for (int i = 0; i < 10; i++)
			{
				TerrainRaycastResult? terrainRaycastResult = m_componentMiner.Raycast<TerrainRaycastResult>(new Ray3(eyePosition, forwardVector + m_random.Vector3(0.75f)), RaycastMode.Interaction);
				if (terrainRaycastResult.HasValue && terrainRaycastResult.Value.Distance < 1.5f && Terrain.ExtractContents(terrainRaycastResult.Value.Value) != 57 && m_componentMiner.Interact(terrainRaycastResult.Value))
				{
					break;
				}
			}
		}

		public override void Load(ValuesDictionary valuesDictionary, IdToEntityMap idToEntityMap)
		{
			m_subsystemTerrain = Project.FindSubsystem<SubsystemTerrain>(throwOnError: true);
			m_subsystemBodies = Project.FindSubsystem<SubsystemBodies>(throwOnError: true);
			m_subsystemMovingBlocks = Project.FindSubsystem<SubsystemMovingBlocks>(throwOnError: true);
			m_subsystemGameInfo = Project.FindSubsystem<SubsystemGameInfo>(throwOnError: true);
			m_subsystemTime = Project.FindSubsystem<SubsystemTime>(throwOnError: true);
			m_subsystemAudio = Project.FindSubsystem<SubsystemAudio>(throwOnError: true);
			m_subsystemSoundMaterials = Project.FindSubsystem<SubsystemSoundMaterials>(throwOnError: true);
			m_subsystemBlockBehaviors = Project.FindSubsystem<SubsystemBlockBehaviors>(throwOnError: true);
			ComponentCreature = Entity.FindComponent<ComponentCreature>(throwOnError: true);
			m_componentHealth = base.Entity.FindComponent<ComponentHealth>();
			m_componentMiner = base.Entity.FindComponent<ComponentMiner>();
			AutoInteractRate = valuesDictionary.GetValue<float>("AutoInteractRate", 0f);
		}
	}
}


