using System;
using System.Xml.Linq;
using Engine;
using GameEntitySystem;
using TemplatesDatabase;

namespace Game
{
	public class MakeFarmerDialog : Dialog
	{
		private ComponentCreature m_creature;
		private ButtonWidget m_yesButton;
		private ButtonWidget m_noButton;

		public MakeFarmerDialog(ComponentCreature creature)
		{
			m_creature = creature;

			XElement node = ContentManager.Get<XElement>("Dialogs/MakeFarmerDialog");
			LoadContents(this, node);

			m_yesButton = Children.Find<ButtonWidget>("MakeFarmerDialog.YesButton", true);
			m_noButton = Children.Find<ButtonWidget>("MakeFarmerDialog.NoButton", true);

			ScrollPanelWidget scrollPanel = Children.Find<ScrollPanelWidget>("MakeFarmerDialog.ScrollPanel", true);
			if (scrollPanel != null)
			{
				scrollPanel.ScrollPosition = 0f;
				scrollPanel.ScrollSpeed = 0f;
			}
		}

		public override void Update()
		{
			if (m_yesButton != null && m_yesButton.IsClicked)
			{
				MakeCreatureFarmer();
				Dismiss();
				return;
			}

			if (m_noButton != null && m_noButton.IsClicked)
			{
				Dismiss();
				return;
			}

			if (Input.Cancel)
			{
				Dismiss();
				return;
			}

			if (m_creature == null || !m_creature.IsAddedToProject || m_creature.ComponentHealth == null || m_creature.ComponentHealth.Health <= 0f)
			{
				Dismiss();
			}
		}

		private void MakeCreatureFarmer()
		{
			if (m_creature == null || !m_creature.IsAddedToProject)
				return;

			if (m_creature.Entity.FindComponent<ComponentFarmerBehavior>() != null)
				return;

			try
			{
				ComponentFarmerBehavior farmerBehavior = new ComponentFarmerBehavior();
				m_creature.Entity.Components.Add(farmerBehavior);
				farmerBehavior.Initialize(m_creature.Entity, new ValuesDictionary());
				farmerBehavior.Load(new ValuesDictionary(), null);
			}
			catch (Exception ex)
			{
				Log.Warning("[ShittyCreatures] Error al agregar ComponentFarmerBehavior: " + ex.Message);
			}
		}

		public void Dismiss()
		{
			DialogsManager.HideDialog(this);
		}
	}
}
