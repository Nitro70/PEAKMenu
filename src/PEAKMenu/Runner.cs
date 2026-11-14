using PEAKMenu.Core;
using PEAKMenu.Features;
using PEAKMenu.UI;
using UnityEngine;
using UnityEngine.InputSystem;

namespace PEAKMenu
{
    // The one MonoBehaviour: keys, feature ticks, input blocking and drawing.
    internal sealed class Runner : MonoBehaviour
    {
        private bool typingInMenu;

        private void Update()
        {
            Settings.Tick();
            WorldCache.Tick();
            Safe.Run("keys", Keys);
            Hub.Tick();
        }

        private void LateUpdate()
        {
            InputBlock.Tick(MainWindow.Open);
            Hub.LateTick();
        }

        private void FixedUpdate()
        {
            Hub.FixedTick();
        }

        private void Keys()
        {
            var kb = Keyboard.current;

            if (kb == null)
            {
                return;
            }

            if (MainWindow.BindTarget != null || MainWindow.BindingMenuKey)
            {
                MainWindow.CaptureBind();
                return;
            }

            if (MainWindow.Open && typingInMenu)
            {
                return;
            }

            var menuKey = kb[Settings.MenuKey.Value];

            if (menuKey != null && menuKey.wasPressedThisFrame)
            {
                MainWindow.Open = !MainWindow.Open;

                if (!MainWindow.Open)
                {
                    Settings.SaveNow();
                }

                return;
            }

            foreach (var bind in Settings.Binds)
            {
                var control = kb[bind.Value];

                if (control != null && control.wasPressedThisFrame && Registry.ById.TryGetValue(bind.Key, out var card))
                {
                    card.Trigger();
                    break;
                }
            }
        }

        private void OnGUI()
        {
            if (!Theme.Ensure())
            {
                return;
            }

            GUI.depth = -1000;
            var oldSkin = GUI.skin;
            GUI.skin = Theme.Skin;

            Safe.Run("esp.draw", Esp.Draw);

            if (MainWindow.Open)
            {
                MainWindow.Draw();
                typingInMenu = GUIUtility.keyboardControl != 0;
            }
            else
            {
                typingInMenu = false;
            }

            Toasts.Draw();
            GUI.skin = oldSkin;
        }

        private void OnApplicationQuit()
        {
            Settings.SaveNow();
        }
    }
}
