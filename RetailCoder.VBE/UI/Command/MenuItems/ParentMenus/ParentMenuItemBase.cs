﻿using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Drawing;
using System.Linq;
using System.Runtime.InteropServices;
using System.Windows.Forms;
using Microsoft.Office.Core;
using Rubberduck.Parsing.VBA;
using stdole;
using NLog;

namespace Rubberduck.UI.Command.MenuItems.ParentMenus
{
    public abstract class ParentMenuItemBase : IParentMenuItem
    {
        private readonly string _key;
        private readonly int? _beforeIndex;
        private readonly IDictionary<IMenuItem, CommandBarControl> _items;
        private static readonly Logger Logger = LogManager.GetCurrentClassLogger();
        private static bool? _useClipboardForMenuIcons;

        protected ParentMenuItemBase(string key, IEnumerable<IMenuItem> items, int? beforeIndex = null)
        {
            _key = key;
            _beforeIndex = beforeIndex;
            _items = items.ToDictionary(item => item, item => null as CommandBarControl);
        }

        public CommandBarControls Parent { get; set; }
        public CommandBarPopup Item { get; private set; }

        public string Key { get { return Item == null ? null : Item.Tag; } }

        public Func<string> Caption { get { return () => Key == null ? null : RubberduckUI.ResourceManager.GetString(Key, Settings.Settings.Culture); } }

        public virtual bool BeginGroup { get { return false; } }
        public virtual int DisplayOrder { get { return default(int); } }

        public void Localize()
        {
            if (Item == null)
            {
                return;
            }
            
            Item.Caption = Caption.Invoke();
            foreach (var kvp in _items)
            {
                kvp.Value.Caption = kvp.Key.Caption.Invoke();
                var command = kvp.Key as CommandMenuItemBase;
                if (command != null)
                {
                    ((CommandBarButton)kvp.Value).ShortcutText = command.Command.ShortcutText;
                }

                var childMenu = kvp.Key as ParentMenuItemBase;
                if (childMenu != null)
                {
                    childMenu.Localize();
                }
            }
        }

        public void Initialize()
        {
            if (Parent == null)
            {
                return;
            }

            Item = _beforeIndex.HasValue
                ? (CommandBarPopup)Parent.Add(MsoControlType.msoControlPopup, Temporary: true, Before: _beforeIndex)
                : (CommandBarPopup)Parent.Add(MsoControlType.msoControlPopup, Temporary: true);
            Item.Tag = _key;

            foreach (var item in _items.Keys.OrderBy(item => item.DisplayOrder))
            {
                _items[item] = InitializeChildControl(item as ICommandMenuItem)
                            ?? InitializeChildControl(item as IParentMenuItem);
            }
        }

        public void RemoveChildren()
        {
            foreach (var child in _items.Keys.Select(item => item as IParentMenuItem).Where(child => child != null))
            {
                child.RemoveChildren();
                Debug.Assert(_items[child] is CommandBarPopup);
                (_items[child] as CommandBarPopup).Delete();
            }
            foreach (var child in _items.Values.Select(item => item as CommandBarButton).Where(child => child != null))
            {
                child.Click -= child_Click;
                child.Delete();
            }
        }

        public void EvaluateCanExecute(RubberduckParserState state)
        {
            foreach (var kvp in _items)
            {
                var parentItem = kvp.Key as IParentMenuItem;
                if (parentItem != null)
                {
                    parentItem.EvaluateCanExecute(state);
                    continue;
                }

                var commandItem = kvp.Key as ICommandMenuItem;
                if (commandItem != null && kvp.Value != null)
                {
                     kvp.Value.Enabled = commandItem.EvaluateCanExecute(state);
                }
            }
        }

        private CommandBarControl InitializeChildControl(IParentMenuItem item)
        {
            if (item == null)
            {
                return null;
            }

            item.Parent = Item.Controls;
            item.Initialize();
            return item.Item;
        }

        private CommandBarControl InitializeChildControl(ICommandMenuItem item)
        {
            if (item == null)
            {
                return null;
            }

            var child = (CommandBarButton)Item.Controls.Add(MsoControlType.msoControlButton, Temporary: true);
            SetButtonImage(child, item.Image, item.Mask);

            child.BeginGroup = item.BeginGroup;
            child.Tag = item.GetType().FullName;
            child.Caption = item.Caption.Invoke();
            var command = item.Command as CommandBase; // todo: add 'ShortcutText' to a new 'interface CommandBase : System.Windows.Input.CommandBase'
            child.ShortcutText = command != null
                ? command.ShortcutText
                : string.Empty;

            child.Click += child_Click;
            return child;
        }

        // note: HAAAAACK!!!
        private static int _lastHashCode;

        private void child_Click(CommandBarButton Ctrl, ref bool CancelDefault)
        {
            var item = _items.Select(kvp => kvp.Key).SingleOrDefault(menu => menu.GetType().FullName == Ctrl.Tag) as ICommandMenuItem;
            if (item == null || Ctrl.GetHashCode() == _lastHashCode)
            {
                return;
            }

            // without this hack, handler runs once for each menu item that's hooked up to the command.
            // hash code is different on every frakkin' click. go figure. I've had it, this is the fix.
            _lastHashCode = Ctrl.GetHashCode();

            Logger.Debug("({0}) Executing click handler for menu item '{1}', hash code {2}", GetHashCode(), Ctrl.Caption, Ctrl.GetHashCode());
            item.Command.Execute(null);
        }

        /// <summary>
        /// Creates a transparent <see cref="IPictureDisp"/> icon for the specified <see cref="CommandBarButton"/>.
        /// </summary>
        public static void SetButtonImage(CommandBarButton button, Image image, Image mask)
        {
            button.FaceId = 0;
            if (image == null || mask == null)
            {
                return;
            }

            {
                _useClipboardForMenuIcons = !HasPictureProperty(button);
            }

            if ((bool)_useClipboardForMenuIcons)
            {
                Bitmap bitMask = MaskedImage(image, mask);
                Clipboard.SetImage(bitMask);
                button.PasteFace();
                Clipboard.Clear();
                return;
            }

            try
            {
                button.Picture = AxHostConverter.ImageToPictureDisp(image);
                button.Mask = AxHostConverter.ImageToPictureDisp(mask);
            }
            catch (COMException exception)
            {
                Logger.Debug("Button image could not be set for button [" + button.Caption + "]\n" + exception);
            }
        }

        private static Bitmap MaskedImage(Image image, Image mask)
        {
            //HACK - just blend image with buttonface color (mask is ignored)
            //TODO - a real solution would use clipboard formats "Toolbar Button Face" AND "Toolbar Button Mask"
            //because PasteFace apparently needs both to be present on the clipboard
            //However, the clipboard formats are apparently only accessible in English versions of Office
            //https://social.msdn.microsoft.com/Forums/office/en-US/33e97c32-9fc2-4531-b208-67c39ccfb048/question-about-toolbar-button-face-in-pasteface-?forum=vsto

            Bitmap output =  new Bitmap(image.Width, image.Height, System.Drawing.Imaging.PixelFormat.Format32bppArgb);

            using (Graphics g = Graphics.FromImage(output)) 
            {
                g.Clear(SystemColors.ButtonFace);
                g.DrawImage(image, 0, 0);
            }
            return output;
        }

        private static bool HasPictureProperty(CommandBarButton button)
        {
            try
            {
                dynamic control = button;
                object picture = control.Picture;
                return true;
            }

            catch (Microsoft.CSharp.RuntimeBinder.RuntimeBinderException exception)
            {
                Logger.Debug("Button image cannot be set for button [" + button.Caption + "], because Host VBE CommandBars are too old.\n" + exception);
            }

            return false;
        }

        private class AxHostConverter : AxHost
        {
            private AxHostConverter() : base(string.Empty) { }

            static public IPictureDisp ImageToPictureDisp(Image image)
            {
                return (IPictureDisp)GetIPictureDispFromPicture(image);
            }

            static public Image PictureDispToImage(IPictureDisp pictureDisp)
            {
                return GetPictureFromIPicture(pictureDisp);
            }
        }
    }
}
