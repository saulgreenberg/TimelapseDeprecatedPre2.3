// PUT THIS INTO THE XAMLE
//   <Window x:Class="Timelapse.Dialog.AdvancedImageSetOptions"
//        xmlns="http://schemas.microsoft.com/winfx/2006/xaml/presentation"
//        xmlns:x="http://schemas.microsoft.com/winfx/2006/xaml"
//        xmlns:controls="clr-namespace:Timelapse.Controls"
//        Title="Advanced Image Set Options" Height="374.5" Width="821.707" >
//    <StackPanel Orientation = "Vertical" Width="811" Margin="2,0,1,3">
//        <controls:StockMessageControl x:Name="Message" Margin="15,10,15,10" Icon="Question"
//                            Title="Advanced Image Set Options"
//                            What="These options should be changed only if you know what you are doing. See the user manual for details." />
//        <GroupBox Header = "Time zone" Margin="15,15,0,10" Padding="10,0,10,0" HorizontalAlignment="Left" VerticalAlignment="Top" FontWeight="Bold" FontSize="12" Height="122" Width="777">
//            <DockPanel>
//                <StackPanel Orientation = "Vertical" Margin="0,0,0,10" >
//                    <TextBlock Text = "By default Timelapse uses your computer's time zone, both when adding files to image sets and when displaying dates and times. If your cameras are in a different time zone or don't follow your local time zone's daylight savings time behavior because your study uses standard or universal time you can select an alternate timezone in the dropdown below." FontWeight="Normal" FontStyle="Normal" FontSize="12"  Width="660" Height="53" TextWrapping="Wrap" />
//                    <StackPanel Orientation = "Horizontal" Margin="0,10,0,0">
//                        <controls:TimeZonePicker x:Name="TimeZones" Width="500" />
//                    </StackPanel>
//                </StackPanel>
//                <Button x:Name="ResetTimeZone" Content="_Reset" FontWeight="Normal" Height="30" Width="58" Margin="0,0,0,10" VerticalAlignment="Bottom" Click="ResetTimeZone_Click" />
//            </DockPanel>
//        </GroupBox>
//        <StackPanel DockPanel.Dock="Bottom" Orientation= "Horizontal" HorizontalAlignment= "Right" >
//            < Button x:Name= "CancelButton" Content= "_Cancel" IsCancel= "True" Margin= "10,10,10,10" Padding= "10" Click= "CancelButton_Click" AutomationProperties.AutomationId= "CancelButton" />
//            < Button x:Name= "OkButton" Content= "_Ok" IsDefault= "True" Margin= "10,10,10,10" Padding= "10" Click= "OkButton_Click" AutomationProperties.AutomationId= "OkButton" />
//        </ StackPanel >
//    </ StackPanel >
//</ Window >

//using System;
//using System.Windows;
//using Timelapse.Database;
//using Timelapse.Util;

//namespace Timelapse.Dialog
//{
//    // DEPRACATED FOR NOW AS I DONT DO ANYTHING WITH THE TIME ZONE
//    public partial class AdvancedImageSetOptions : Window
//    {
//        //private readonly TimeZoneInfo currentImageSetTimeZone;
//        //private readonly FileDatabase database;

//        public AdvancedImageSetOptions(FileDatabase database, Window owner)
//        {
//// Check the arguments for null 
//ThrowIf.IsNullArgument(database, nameof(database));

//this.InitializeComponent();
//this.Owner = owner;
//this.database = database;

//this.currentImageSetTimeZone = database.ImageSet.GetSystemTimeZone();
//this.TimeZones.SelectedItem = this.currentImageSetTimeZone.DisplayName;
//}

//private void CancelButton_Click(object sender, RoutedEventArgs e)
//{
//    this.DialogResult = false;
//}

//private void OkButton_Click(object sender, RoutedEventArgs e)
//{
//    this.database.ImageSet.TimeZone = this.TimeZones.TimeZonesByDisplayName[(string)this.TimeZones.SelectedItem].Id;
//    this.database.UpdateSyncImageSetToDatabase();

//    this.DialogResult = true;
//}

//private void ResetTimeZone_Click(object sender, RoutedEventArgs e)
//{
//    this.TimeZones.SelectedItem = this.currentImageSetTimeZone;
//}
//    }
//}