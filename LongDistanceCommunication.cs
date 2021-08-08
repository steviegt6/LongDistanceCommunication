using System;
using Mono.Cecil.Cil;
using MonoMod.Cil;
using Terraria;
using Terraria.Audio;
using Terraria.ID;
using TomatoLib;
using TomatoLib.Core.Utilities.Extensions;

namespace LongDistanceCommunication
{
	public class LongDistanceCommunication : TomatoMod
    {
        public bool[] Hovering = new bool[Main.npc.Length];

        public override void Load()
        {
            base.Load();
            
            IL.Terraria.Main.DrawMap += ResizeNpcHoverHeads;
            IL.Terraria.Player.Update += PreventNpcChatExit;
        }

        public override void Unload()
        {
            base.Unload();
            
            IL.Terraria.Main.DrawMap -= ResizeNpcHoverHeads;
            IL.Terraria.Player.Update -= PreventNpcChatExit;
        }

        private void ResizeNpcHoverHeads(ILContext il)
        {
            ILCursor c = new(il);

            if (!c.TryGotoNext(x => x.MatchLdloca(305)))
            {
                ModLogger.PatchFailure("Terraria.Main", "DrawMap", "ldloca", "305");
                return;
            }

            if (!c.TryGotoNext(MoveType.After, x => x.MatchStloc(0)))
            {
                ModLogger.PatchFailure("Terraria.Main", "DrawMap", "stloc", "0");
                return;
            }

            c.Index += 2;

            c.Emit(OpCodes.Ldloc, 311);
            c.Emit(OpCodes.Ldloc, 313);
            c.Emit(OpCodes.Ldloc, 312);
            c.Emit(OpCodes.Ldloc, 314);

            c.EmitDelegate<Action<NPC[], int, float, float, float, float>>((npcArray, npcIndex, lesserX, greaterX, lesserY, greaterY) =>
            {
                if (!Main.LocalPlayer.HasItem(ItemID.CellPhone))
                {
                    Hovering[npcIndex] = false;
                    return;
                }

                if (Main.mouseX >= lesserX && Main.mouseX <= greaterX &&
                    Main.mouseY >= lesserY && Main.mouseY <= greaterY)
                {
                    if (!Hovering[npcIndex])
                        SoundEngine.PlaySound(SoundID.MenuTick);

                    Hovering[npcIndex] = true;
                }
                else
                    Hovering[npcIndex] = false;

                if (!Hovering[npcIndex] || !Main.mouseLeft || !Main.mouseLeftRelease)
                    return;

                Main.mapFullscreen = false;
                Main.LocalPlayer.SetTalkNPC(npcIndex);
                Main.npcChatText = npcArray[npcIndex].GetChat();
                Main.LocalPlayer.chest = Main.LocalPlayer.sign = -1;
                Main.editSign = false;
                Main.npcChatCornerItem = 0;
                Recipe.FindRecipes();
                SoundEngine.PlaySound(SoundID.Chat);
            });

            c.Emit(OpCodes.Ldsfld, typeof(Main).GetCachedField("npc"));
            c.Emit(OpCodes.Ldloc, 293);

            if (!c.TryGotoNext(MoveType.After, x => x.MatchLdloc(235)))
            {
                ModLogger.PatchFailure("Terraria.Main", "DrawMap", "ldloc", "235");
                return;
            }

            // op-code before us:
            // c.Emit(OpCodes.Ldloc, 235); // npc head draw scale

            c.Emit(OpCodes.Ldloc, 293);

            c.EmitDelegate<Func<float, int, float>>((scale, npcIndex) => scale * (Hovering[npcIndex] ? 1.25f : 1f));

            // c.Emit(OpCodes.Stloc, 235);
        }

        private void PreventNpcChatExit(ILContext il)
        {
            ILCursor c = new(il);

            if (!c.TryGotoNext(MoveType.After, x => x.MatchCall<Player>("SetTalkNPC")))
            {
                ModLogger.PatchFailure("Terraria.Player", "Update", "call", "Terraria.Player::SetTalkNPC");
                return;
            }

            c.Index -= 3;
            c.RemoveRange(8);

            c.EmitDelegate<Action<Player>>(player =>
            {
                if (player.HasItem(ItemID.CellPhone))
                    return;

                player.SetTalkNPC(-1);
                Main.npcChatCornerItem = 0;
                Main.npcChatText = "";
            });

            if (!c.TryGotoPrev(MoveType.After, x => x.MatchLdcI4(11)))
            {
                ModLogger.PatchFailure("Terraria.Player", "Update", "ldc.i4.s", "11");
                return;
            }

            c.Emit(OpCodes.Pop);
            c.Emit(OpCodes.Ldc_I4_M1);
        }
    }
}