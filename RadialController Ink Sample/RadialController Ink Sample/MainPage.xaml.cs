using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Runtime.InteropServices.WindowsRuntime;
using Windows.Foundation;
using Windows.Foundation.Collections;
using Windows.UI.Xaml;
using Windows.UI.Xaml.Controls;
using Windows.UI.Xaml.Controls.Primitives;
using Windows.UI.Xaml.Data;
using Windows.UI.Xaml.Input;
using Windows.UI.Xaml.Media;
using Windows.UI.Xaml.Navigation;
using Windows.UI.Input;
using Windows.Storage.Streams;

// The Blank Page item template is documented at http://go.microsoft.com/fwlink/?LinkId=402352&clcid=0x409

namespace RadialController_Ink_Sample
{
    public sealed partial class MainPage : Page
    {
        #region RadialController Setup
        RadialController myController;
        bool isRightHanded;

        public MainPage()
        {
            this.InitializeComponent();
            UpdatePreview();
            highlightedItem = RValue;
            RValue.ValueChanged += Slider_ValueChanged;
            GValue.ValueChanged += Slider_ValueChanged;
            BValue.ValueChanged += Slider_ValueChanged;

            //Hide our custom tool's UI until it is activated
            ToolPanel.Visibility = Visibility.Collapsed;

            // Create a reference to the RadialController.
            myController = RadialController.CreateForCurrentView();

            // Create a menu item for the custom tool.
            RadialControllerMenuItem myItem =
              RadialControllerMenuItem.CreateFromKnownIcon("Background", RadialControllerMenuKnownIcon.InkColor);

            //Add the custom tool's menu item to the menu
            myController.Menu.Items.Add(myItem);

            //Create a handler for when the menu item is selected
            myItem.Invoked += MyItem_Invoked;

            //Create handlers for button and rotational input
            myController.RotationChanged += MyController_RotationChanged;
            myController.ButtonClicked += MyController_ButtonClicked;

            //Remove Scroll/Zoom/Undo tools as app doesn't support them
            RadialControllerConfiguration config = RadialControllerConfiguration.GetForCurrentView();
            config.SetDefaultMenuItems(new RadialControllerSystemMenuItemKind[] { RadialControllerSystemMenuItemKind.Volume });

            //Query user's handedness for on-screen UI
            Windows.UI.ViewManagement.UISettings settings = new Windows.UI.ViewManagement.UISettings();
            isRightHanded = (settings.HandPreference == Windows.UI.ViewManagement.HandPreference.RightHanded);

            //Create handlers for when RadialController provides an on-screen position
            myController.ScreenContactStarted += MyController_ScreenContactStarted;
            myController.ScreenContactContinued += MyController_ScreenContactContinued;
            myController.ScreenContactEnded += MyController_ScreenContactEnded;

            //Create handlers for when RadialController focus changes
            myController.ControlAcquired += MyController_ControlAcquired;
            myController.ControlLost += MyController_ControlLost;
        }
        #endregion

        #region Handling RadialController Input
        private void MyItem_Invoked(RadialControllerMenuItem sender, object args)
        {
            //Make RGB panel visible when the custom menu item is invoked
            ToolPanel.Visibility = Visibility.Visible;
        }

        Slider selectedItem = null;
        FrameworkElement highlightedItem = null;

        private void MyController_ButtonClicked(RadialController sender, RadialControllerButtonClickedEventArgs args)
        {
            if (highlightedItem == Preview)
            {
                //Click on the Preview, update the background
                UpdateBackground();
            }

            else if (selectedItem != null)
            {
                //Click on a selected slider, unselect the slider
                selectedItem = null;
                UpdateHighlight(highlightedItem);
                //decrease sensitivity to make it more comfortable to navigate between items
                myController.RotationResolutionInDegrees = 10;
            }

            else if (selectedItem == null)
            {
                //No selection, select a slider
                UpdateSelection(highlightedItem as Slider);
                //increase sensitivity to make it easier to change slider value
                myController.RotationResolutionInDegrees = 1;
            }
        }

        private void MyController_RotationChanged(RadialController sender, RadialControllerRotationChangedEventArgs args)
        {
            if (selectedItem != null)
            {
                //Change the value on the slider
                selectedItem.Value += args.RotationDeltaInDegrees;
            }
            else if (args.RotationDeltaInDegrees > 0)
            {
                //Rotation is to the right, change the highlighted item accordingly
                if (highlightedItem == RValue)
                {
                    UpdateHighlight(GValue);
                }
                else if (highlightedItem == GValue)
                {
                    UpdateHighlight(BValue);
                }
                else if (highlightedItem == BValue)
                {
                    UpdateHighlight(Preview);
                }
            }
            else if (args.RotationDeltaInDegrees < 0)
            {
                //Rotation is to the left, change the highlighted item accordingly
                if (highlightedItem == GValue)
                {
                    UpdateHighlight(RValue);
                }
                else if (highlightedItem == BValue)
                {
                    UpdateHighlight(GValue);
                }
                else if (highlightedItem == Preview)
                {
                    UpdateHighlight(BValue);
                }
            }
        }

        private void UpdateHighlight(FrameworkElement element)
        {
            StackPanel parent;

            //Remove highlight state from previous element
            if (highlightedItem != null)
            {
                parent = highlightedItem.Parent as StackPanel;
                parent.BorderThickness = new Thickness(0);
            }

            //Update highlight state for new element
            highlightedItem = element;

            parent = highlightedItem.Parent as StackPanel;
            parent.BorderBrush = new SolidColorBrush(Windows.UI.Colors.Black);
            parent.BorderThickness = new Thickness(2);
        }

        private void UpdateSelection(Slider element)
        {
            selectedItem = element;

            //Update selection state for selected slider
            StackPanel parent = element.Parent as StackPanel;
            parent.BorderBrush = new SolidColorBrush(Windows.UI.Colors.Cyan);
            parent.BorderThickness = new Thickness(4);
        }

        private void Slider_ValueChanged(object sender, RangeBaseValueChangedEventArgs e)
        {
            UpdatePreview();
        }

        private void UpdatePreview()
        {
            Windows.UI.Color selectedColor = new Windows.UI.Color();
            selectedColor.A = 255;
            selectedColor.R = (byte)RValue.Value;
            selectedColor.G = (byte)GValue.Value;
            selectedColor.B = (byte)BValue.Value;

            Preview.Background = new SolidColorBrush(selectedColor);
        }

        private void UpdateBackground()
        {
            CanvasGrid.Background = Preview.Background;
        }

        #endregion

        #region Building On-Screen UI
        private void MyController_ScreenContactStarted(RadialController sender, RadialControllerScreenContactStartedEventArgs args)
        {
            UpdatePanelLocation(args.Contact);
        }

        private void MyController_ScreenContactContinued(RadialController sender, RadialControllerScreenContactContinuedEventArgs args)
        {
            UpdatePanelLocation(args.Contact);
        }

        private void MyController_ScreenContactEnded(RadialController sender, object args)
        {
            ResetPanelLocation();
        }

        private void UpdatePanelLocation(RadialControllerScreenContact contact)
        {
            //When an on-screen position is provided, apply a transform to the panel
            TranslateTransform x = new TranslateTransform();
            if (isRightHanded)
            {
                //Render to the right of the RadialController
                x.X = contact.Position.X + contact.Bounds.Width / 2 + 50;
            }
            else
            {
                //Render to the left of the RadialController
                x.X = contact.Position.X - contact.Bounds.Width / 2 - 50 - ToolPanel.Width;
            }
            x.Y = contact.Position.Y - 200;
            ToolPanel.RenderTransform = x;
            ToolPanel.HorizontalAlignment = HorizontalAlignment.Left;
        }
        private void ResetPanelLocation()
        {
            //When an on-screen position is not provided, clear the transform on the panel
            ToolPanel.RenderTransform = null;
            ToolPanel.HorizontalAlignment = HorizontalAlignment.Right;
        }

        private void MyController_ControlAcquired(RadialController sender, RadialControllerControlAcquiredEventArgs args)
        {
            //Ensure tool panel is rendered at the correct location when focus is gained
            if (args.Contact != null)
            {
                UpdatePanelLocation(args.Contact);
            }

            ToolPanel.Visibility = Visibility.Visible;
        }

        private void MyController_ControlLost(RadialController sender, object args)
        {
            //Hide tool panel when focus is lost
            ToolPanel.Visibility = Visibility.Collapsed;
            ResetPanelLocation();
        }

        #endregion


    }
}
