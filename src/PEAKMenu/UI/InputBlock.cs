using UnityEngine;

namespace PEAKMenu.UI
{
    // While the menu is open the game must think one of its own windows is open: that frees the
    // mouse and stops your scout walking and looking around (Patches hooks GUIManager's window
    // check for that). This only makes sure the cursor is free even on screens without GUIManager.
    // When the menu closes the game's own cursor handler locks it again by itself.
    internal static class InputBlock
    {
        public static void Tick(bool open)
        {
            if (open)
            {
                Cursor.lockState = CursorLockMode.None;
                Cursor.visible = true;
            }
        }
    }
}
