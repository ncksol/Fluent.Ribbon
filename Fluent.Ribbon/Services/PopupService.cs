﻿// ReSharper disable once CheckNamespace
namespace Fluent
{
    using System;
    using System.Diagnostics;
    using System.Windows;
    using System.Windows.Controls;
    using System.Windows.Controls.Primitives;
    using System.Windows.Input;
    using Fluent.Extensions;
    using Fluent.Internal;

    /// <summary>
    /// Dismiss popup mode
    /// </summary>
    public enum DismissPopupMode
    {
        /// <summary>
        /// Always dismiss popup
        /// </summary>
        Always,

        /// <summary>
        /// Dismiss only if mouse is not over popup
        /// </summary>
        MouseNotOver
    }

    /// <summary>
    /// Dismiss popup arguments
    /// </summary>
    public class DismissPopupEventArgs : RoutedEventArgs
    {
        /// <summary>
        /// Standard constructor
        /// </summary>
        public DismissPopupEventArgs()
            : this(DismissPopupMode.Always)
        {
        }

        /// <summary>
        /// Constructor
        /// </summary>
        /// <param name="dismissMode">Dismiss mode</param>
        public DismissPopupEventArgs(DismissPopupMode dismissMode)
        {
            this.RoutedEvent = PopupService.DismissPopupEvent;
            this.DismissMode = dismissMode;
        }

        /// <summary>
        /// Popup dismiss mode
        /// </summary>
        public DismissPopupMode DismissMode { get; set; }

        /// <inheritdoc />
        protected override void InvokeEventHandler(Delegate genericHandler, object genericTarget)
        {
            var handler = (EventHandler<DismissPopupEventArgs>)genericHandler;
            handler(genericTarget, this);
        }
    }

    /// <summary>
    /// Represent additional popup functionality
    /// </summary>
    public static class PopupService
    {
        #region DismissPopup

        /// <summary>
        /// Occurs then popup is dismissed
        /// </summary>
        public static readonly RoutedEvent DismissPopupEvent = EventManager.RegisterRoutedEvent("DismissPopup", RoutingStrategy.Bubble, typeof(EventHandler<DismissPopupEventArgs>), typeof(PopupService));

        /// <summary>
        /// Raises DismissPopup event (Async)
        /// </summary>
        public static void RaiseDismissPopupEventAsync(object sender, DismissPopupMode mode)
        {
            var element = sender as UIElement;

            if (element == null)
            {
                return;
            }

            Debug.WriteLine("Dismissing Popup (async)");

            element.RunInDispatcherAsync(() => RaiseDismissPopupEvent(sender, mode));
        }

        /// <summary>
        /// Raises DismissPopup event
        /// </summary>
        public static void RaiseDismissPopupEvent(object sender, DismissPopupMode mode)
        {
            var element = sender as UIElement;

            if (element == null)
            {
                return;
            }

            Debug.WriteLine("Dismissing Popup");

            element.RaiseEvent(new DismissPopupEventArgs(mode));
        }

        #endregion

        /// <summary>
        /// Set needed parameters to control
        /// </summary>
        /// <param name="classType">Control type</param>
        public static void Attach(Type classType)
        {
            EventManager.RegisterClassHandler(classType, Mouse.PreviewMouseDownOutsideCapturedElementEvent, new MouseButtonEventHandler(OnClickThroughThunk));
            EventManager.RegisterClassHandler(classType, DismissPopupEvent, new EventHandler<DismissPopupEventArgs>(OnDismissPopup));
            EventManager.RegisterClassHandler(classType, FrameworkElement.ContextMenuOpeningEvent, new ContextMenuEventHandler(OnContextMenuOpened), true);
            EventManager.RegisterClassHandler(classType, FrameworkElement.ContextMenuClosingEvent, new ContextMenuEventHandler(OnContextMenuClosed), true);
            EventManager.RegisterClassHandler(classType, UIElement.LostMouseCaptureEvent, new MouseEventHandler(OnLostMouseCapture));
        }

        /// <summary>
        /// Handles PreviewMouseDownOutsideCapturedElementEvent event
        /// </summary>
        public static void OnClickThroughThunk(object sender, MouseButtonEventArgs e)
        {
            ////Debug.WriteLine(string.Format("OnClickThroughThunk: sender = {0}; originalSource = {1}; mouse capture = {2}", sender, e.OriginalSource, Mouse.Captured));

            if (e.ChangedButton == MouseButton.Left
                || e.ChangedButton == MouseButton.Right)
            {
                if (Mouse.Captured == sender
                    // Special handling for unknown Popups (for example datepickers used in the ribbon)
                    || (sender is IDropDownControl && IsPopupRoot(Mouse.Captured)))
                {
                    if (sender is RibbonTabControl ribbonTabControl
                        && ribbonTabControl.IsMinimized
                        // this is true if, for example, a DatePicker popup is open and we click outside of the ribbon popup
                        // this should then only close the DatePicker popup but not the ribbon popup
                        && IsPopupRoot(e.OriginalSource) == false)
                    {
                        // Don't close the ribbon popup if the mouse is over the ribbon popup
                        if (IsMousePhysicallyOver(ribbonTabControl.SelectedContentPresenter) == false)
                        {
                            // Force dismissing the Ribbon-Popup.
                            // Always is needed because of eager-closing-prevention.
                            RaiseDismissPopupEvent(sender, DismissPopupMode.Always);
                        }
                    }
                    else
                    {
                        RaiseDismissPopupEvent(sender, DismissPopupMode.MouseNotOver);
                    }
                }
            }
        }

        /// <summary>
        /// Handles lost mouse capture event
        /// </summary>
        public static void OnLostMouseCapture(object sender, MouseEventArgs e)
        {
            Debug.WriteLine($"Sender         - {sender}");
            Debug.WriteLine($"OriginalSource - {e.OriginalSource}");
            Debug.WriteLine($"Mouse.Captured - {Mouse.Captured}");

            var control = sender as IDropDownControl;

            if (control == null)
            {
                return;
            }

            if (Mouse.Captured != sender
                && control.IsDropDownOpen
                && !control.IsContextMenuOpened)
            {
                var popup = control.DropDownPopup;

                if (popup?.Child == null)
                {
                    RaiseDismissPopupEvent(sender, DismissPopupMode.MouseNotOver);
                    return;
                }

                if (e.OriginalSource == sender)
                {
                    // If Ribbon loses capture because something outside popup is clicked - close the popup
                    if (Mouse.Captured == null
                        || IsAncestorOf(popup.Child, Mouse.Captured as DependencyObject) == false)
                    {
                        RaiseDismissPopupEvent(sender, DismissPopupMode.MouseNotOver);
                    }

                    return;
                }

                if (IsAncestorOf(popup.Child, e.OriginalSource as DependencyObject) == false)
                {
                    RaiseDismissPopupEvent(sender, DismissPopupMode.MouseNotOver);
                    return;
                }

                // This code is needed to keep some popus open.
                // One of these is the ribbon popup when it's minimized.
                if (e.OriginalSource != null
                    && Mouse.Captured == null
                    && (IsPopupRoot(e.OriginalSource) || IsAncestorOf(popup.Child, e.OriginalSource as DependencyObject)))
                {
                    Debug.WriteLine($"Setting mouse capture to: {sender}");
                    Mouse.Capture(sender as IInputElement, CaptureMode.SubTree);
                    e.Handled = true;

                    // Only raise a popup dismiss event if the source is MenuBase.
                    // this is because MenuBase "steals" the mouse focus in a way we have to work around here.
                    if (e.OriginalSource is MenuBase)
                    {
                        RaiseDismissPopupEvent(sender, DismissPopupMode.MouseNotOver);
                    }
                }
            }
        }

        /// <summary>
        /// Returns true whether parent is ancestor of element
        /// </summary>
        /// <param name="parent">Parent</param>
        /// <param name="element">Element</param>
        /// <returns>Returns true whether parent is ancestor of element</returns>
        public static bool IsAncestorOf(DependencyObject parent, DependencyObject element)
        {
            while (element != null)
            {
                if (ReferenceEquals(element, parent))
                {
                    return true;
                }

                element = UIHelper.GetVisualOrLogicalParent(element);
            }

            return false;
        }

        /// <summary>
        /// Handles dismiss popup event
        /// </summary>
        public static void OnDismissPopup(object sender, DismissPopupEventArgs e)
        {
            var control = sender as IDropDownControl;

            if (control == null)
            {
                return;
            }

            if (e.DismissMode == DismissPopupMode.Always)
            {
                if (Mouse.Captured == control)
                {
                    Mouse.Capture(null);
                }

                control.IsDropDownOpen = false;
            }
            else if (control.IsDropDownOpen)
            {
                // Prevent eager closing of the Ribbon-Popup and forward mouse focus to the ribbon popup instead.
                if (control is RibbonTabControl ribbonTabControl
                    && ribbonTabControl.IsMinimized
                    && IsAncestorOf(control as DependencyObject, e.OriginalSource as DependencyObject))
                {
                    Mouse.Capture(control as IInputElement, CaptureMode.SubTree);

                    return;
                }

                if (IsMousePhysicallyOver(control.DropDownPopup.Child) == false)
                {
                    if (Mouse.Captured == control)
                    {
                        Mouse.Capture(null);
                    }

                    control.IsDropDownOpen = false;
                }
                else
                {
                    if (Mouse.Captured != control)
                    {
                        Mouse.Capture(sender as IInputElement, CaptureMode.SubTree);
                    }

                    e.Handled = true;
                }
            }
        }

        /// <summary>
        /// Returns true whether mouse is physically over the popup
        /// </summary>
        /// <param name="popup">Element</param>
        /// <returns>Returns true whether mouse is physically over the popup</returns>
        public static bool IsMousePhysicallyOver(Popup popup)
        {
            if (popup?.Child == null)
            {
                return false;
            }

            return IsMousePhysicallyOver(popup.Child);
        }

        /// <summary>
        /// Returns true whether mouse is physically over the element
        /// </summary>
        /// <param name="element">Element</param>
        /// <returns>Returns true whether mouse is physically over the element</returns>
        public static bool IsMousePhysicallyOver(UIElement element)
        {
            if (element == null)
            {
                return false;
            }

            var position = Mouse.GetPosition(element);
            return position.X >= 0.0
                && position.Y >= 0.0
                && position.X <= element.RenderSize.Width
                && position.Y <= element.RenderSize.Height;
        }

        /// <summary>
        /// Handles context menu opened event
        /// </summary>
        public static void OnContextMenuOpened(object sender, ContextMenuEventArgs e)
        {
            if (sender is IDropDownControl control)
            {
                control.IsContextMenuOpened = true;
                // Debug.WriteLine("Context menu opened");
            }
        }

        /// <summary>
        /// Handles context menu closed event
        /// </summary>
        public static void OnContextMenuClosed(object sender, ContextMenuEventArgs e)
        {
            if (sender is IDropDownControl control)
            {
                //Debug.WriteLine("Context menu closed");
                control.IsContextMenuOpened = false;
                RaiseDismissPopupEvent(control, DismissPopupMode.MouseNotOver);
            }
        }

        private static bool IsPopupRoot(object obj)
        {
            if (obj == null)
            {
                return false;
            }

            var type = obj.GetType();

            return type.FullName == "System.Windows.Controls.Primitives.PopupRoot"
                   || type.Name == "PopupRoot";
        }
    }
}