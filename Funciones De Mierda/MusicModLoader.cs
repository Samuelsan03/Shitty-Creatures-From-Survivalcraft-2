using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Runtime.CompilerServices;
using System.Runtime.InteropServices;
using System.Threading.Tasks.Dataflow;
using Engine;
using Engine.Graphics;
using Engine.Media;
using Game;
using GameEntitySystem;

namespace Game
{
	public class MusicModLoader : ModLoader
	{
		// Variable estática para evitar repetir la misma canción
		private static string lastPlayedTrack = "";
		private static Game.Random random = new Game.Random();

		public override void __ModInitialize()
		{
			ModsManager.RegisterHook("MenuPlayMusic", this);
		}

		public override void MenuPlayMusic(out string contentMusicPath)
		{
			string[] musicTracks = new string[]
			{
				"MenuMusic/Digimon 02 Target Wada Kouji",
				"MenuMusic/Touhou 2 Mimas Theme Complete Darkness",
				"MenuMusic/Touhou 2 Eastern Wind",
				"MenuMusic/Touhou 2 Record of the Sealing of an Oriental Demon",
				"MenuMusic/Digimon 02 Evolution Break Up Ayumi Miyazaki",
				"MenuMusic/Digimon Adventure 01 Brave Heart Wada Kouji",
				"MenuMusic/Digimon Adventure 01 Butterfly Wada Kouji",
				"MenuMusic/Digimon Savers OP1 Theme Song Gouing Going My Soul Dynamite SHU",
				"MenuMusic/Digimon Savers OP2 Hirari Wada Kouji",
				"MenuMusic/EoSD Credits Theme Crimson Belvedere Eastern Dream",
				"MenuMusic/Digimon Tamers The Biggest Dreamer Wada Kouji",
				"MenuMusic/Touhou 6 Flandre Scarlets Theme U.N. Owen was her",
				"MenuMusic/Digimon Frontiers FIRE Wada Kouji",
				"MenuMusic/Rocket Knight Adventures Stage 1-1",
				"MenuMusic/Rocket Knight Adventures Stage 1-2",
				"MenuMusic/Rocket Knight Adventures 2 Stage 1-1",
				"MenuMusic/Sparkster Stage Lakeside",
				"MenuMusic/Space Harrier Theme",
				"MenuMusic/MAGICAL SOUND SHOWER OutRun",
				"MenuMusic/Super Hang-On Outride A Crisis",
				"MenuMusic/Super Hang-On Sprinter",
				"MenuMusic/Super Hang-On Winning Run"
			};

			// Usar Environment.TickCount como semilla simple
			int index = Math.Abs(Environment.TickCount) % musicTracks.Length;

			string selectedTrack = musicTracks[index];

			// Evitar repetir la misma canción
			if (selectedTrack == lastPlayedTrack && musicTracks.Length > 1)
			{
				index = (index + 1) % musicTracks.Length;
				selectedTrack = musicTracks[index];
			}

			lastPlayedTrack = selectedTrack;
			contentMusicPath = selectedTrack;
		}
	}
}
