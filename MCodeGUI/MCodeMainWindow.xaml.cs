using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;
using System.IO;
using REScan.Common;
using REScan.Data;
using REScan.IO;


namespace MCodeGUI {

    public class ProcessStats {
        public int TotalMeasurementsForInterpolation = 0;
        public int InterpolatedMeasurements = 0;
        public int TotalMeasurementsForReformatting = 0;
        public int ReformattedMeasurements = 0;
    }
    
    public partial class MCodeMainWindow : Window {
        private Brush brushWarning;
        private Brush brushOk;

        ProcessStats DasStats;
        ProcessStats GsmStats;
        ProcessStats WcdmaStats;
        ProcessStats LteStats;

        enum DataAggregation {
            Tech,
            TechHeight,
            None
        };
        DataAggregation AggroType;

        private List<string> OutputFileNames;

        private void Log(string str) {
            OutputListBox.Items.Add(str);
        }

        public MCodeMainWindow() {
            InitializeComponent();
            this.Title = "MCode";
            //if (Properties.Settings.Default.DataCollation == 6847631940225027084)
            //{
            //    //mcode.MCodeClass.DasTx.encrypt = false;
            this.Title += " - Encryption Bypassed";
            //}
            //else
            //    //mcode.MCodeClass.DasTx.encrypt = true;
            OutputFileNames = new List<string>();
        }
        private void OnException(Exception exc) {
            string msg = exc.Message;
            while(exc.InnerException != null) {
                msg += System.Environment.NewLine;
                msg += exc.InnerException.Message;
                exc = exc.InnerException;
            }
            MessageBox.Show(msg, "MCode Error :", MessageBoxButton.OK, MessageBoxImage.Error);
        }

        private void ChooseInterpolationFilesButton_Click(object sender, RoutedEventArgs e) {
            var extension = ".wpt";
            var filter = "Waypoint Files|*.wpt";
            SelectDataFiles(extension, filter, InterpolationFilesListBox);
        }

        private void SelectDataFiles(string extension, string filter, ListBox listBox) {
            try {
                // Create OpenFileDialog 
                Microsoft.Win32.OpenFileDialog dlg = new Microsoft.Win32.OpenFileDialog();

                // Set filter for file extension and default file extension 
                dlg.DefaultExt = extension;
                dlg.Filter = filter;
                dlg.Multiselect = true;

                // Display OpenFileDialog by calling ShowDialog method 
                Nullable<bool> result = dlg.ShowDialog();

                // Get the selected file name and display in a TextBox 
                if(result == true) {
                    int num_files = dlg.FileNames.Count();
                    for(int idx = 0; idx < num_files; idx++) {
                        // Open document 
                        string filename = dlg.FileNames[idx];
                        listBox.Items.Add(filename);
                    }
                }
            }
            catch(Exception exc) {
                OnException(exc);
            }
        }

        private void ProcessButton_Click(object sender, RoutedEventArgs e)
        {
            try
            {
                ProcessButton.IsEnabled = false;

                OutputFileNames.Clear();

                string aggregatedFileName = "";
                var isFeetToMeters = Convert.ToBoolean(ConvertHeightToMeters.IsChecked);

                if(Convert.ToBoolean(IsAggregatedByTechHeight.IsChecked)) {
                    AggroType = DataAggregation.TechHeight;
                    aggregatedFileName = ChooseOutputFileButton_Click(sender, e);
                    if(aggregatedFileName.Count() == 0)
                        return;
                    Log("Aggregating data based on technology and height.");
                }
                else if(Convert.ToBoolean(IsAggregatedByTech.IsChecked)) {
                    AggroType = DataAggregation.Tech;
                    aggregatedFileName = ChooseOutputFileButton_Click(sender, e);
                    if(aggregatedFileName.Count() == 0)
                        return;
                    Log("Aggregating data based on technology.");
                }
                else {
                    AggroType = DataAggregation.None;
                    Log("No data aggregation.");
                }

                if(isFeetToMeters) 
                    Log("Converting feet to meters."); 
                else
                    Log("No height conversion needed.");

                var interpolator = new REScan.MCode.Interpolator();
                var wptIO = new WaypointIO();
                var dasIO = new DasIO();
                var gsmIO = new GsmIO();
                var wcdmaIO = new WcdmaIO();
                var lteIO = new LteIO();

                var InterpolationFiles = InterpolationFilesListBox.Items.OfType<string>().ToList();
                foreach(var wptFileName in InterpolationFiles) {
                        if(wptIO.IsEmpty(wptFileName)) {
                            Log("Found empty waypoint file. Skipping... Filename: " + wptFileName + ".");
                            continue;
                        }

                        var wptMeasurements = wptIO.ReadFile(wptFileName);
                        if(wptMeasurements.Count != 0) {

                            if(isFeetToMeters) {
                                foreach(var meas in wptMeasurements) {
                                    meas.Height = System.Convert.ToInt32(meas.Height * 0.3048);
                                }
                            }

                            try {
                               OutputInterpolatedFile(aggregatedFileName, AggroType, interpolator, dasIO, wptFileName, wptMeasurements, DasStats);
                            } catch(Exception ex) {
                                Log("Error while interpolating DAS using \"" + wptFileName + "\": "  + ex.Message);
                            }
                            try {
                                OutputInterpolatedFile(aggregatedFileName, AggroType, interpolator, gsmIO, wptFileName, wptMeasurements, GsmStats);
                            } catch(Exception ex) {
                                Log("Error while interpolating GSM using \"" + wptFileName + "\": " + ex.Message);
                            }
                            try {
                                OutputInterpolatedFile(aggregatedFileName, AggroType, interpolator, wcdmaIO, wptFileName, wptMeasurements, WcdmaStats);
                            } catch(Exception ex) {
                                Log("Error while interpolating UMTS using \"" + wptFileName + "\": " + ex.Message);
                            }
                            try {
                                OutputInterpolatedFile(aggregatedFileName, AggroType, interpolator, lteIO, wptFileName, wptMeasurements, LteStats);
                            } catch (Exception ex) {
                                Log("Error while interpolating LTE using \"" + wptFileName + "\": " + ex.Message);
                            }
                        }
                }

                var ReformatFiles = ReformatFilesListBox.Items.OfType<string>().ToList();
                foreach (var measFileName in ReformatFiles)
                {
                    try {
                        OutputReformattedFile(aggregatedFileName, AggroType, dasIO, measFileName, DasStats);
                    } catch (Exception ex) {
                        Log("Error while reformatting \"" + measFileName + "\": " + ex.Message);
                    }

                    try {
                        OutputReformattedFile(aggregatedFileName, AggroType, gsmIO, measFileName, GsmStats);
                    } catch (Exception ex) {
                        Log("Error while reformatting \"" + measFileName + "\": " + ex.Message);
                    } 

                    try {
                        OutputReformattedFile(aggregatedFileName, AggroType, wcdmaIO, measFileName, WcdmaStats);
                    } catch (Exception ex) {
                        Log("Error while reformatting \"" + measFileName + "\": " + ex.Message);
                    }

                    try {
                        OutputReformattedFile(aggregatedFileName, AggroType, lteIO, measFileName, LteStats);
                    } catch (Exception ex) {
                        Log("Error while reformatting \"" + measFileName + "\": " + ex.Message);
                    }
                }
                OutputIOLogs(dasIO, gsmIO, wcdmaIO, lteIO);

                MessageBox.Show("Finished processing!", "M-Code Ok", MessageBoxButton.OK, MessageBoxImage.Asterisk);
            }
            catch (Exception exc)
            {
                OnException(exc);
            }
            ProcessButton.IsEnabled = true;
        }

        private void OutputIOLogs(DasIO dasIO, GsmIO gsmIO, WcdmaIO wcdmaIO, LteIO lteIO) {
            if(dasIO.ErrorList.Count > 0) { foreach(var str in dasIO.ErrorList) { Log(str); } }
            if(gsmIO.ErrorList.Count > 0) { foreach(var str in gsmIO.ErrorList) { Log(str); } }
            if(wcdmaIO.ErrorList.Count > 0) { foreach(var str in wcdmaIO.ErrorList) { Log(str); } }
            if(lteIO.ErrorList.Count > 0) { foreach(var str in lteIO.ErrorList) { Log(str); } }
        }

        private void OutputInterpolatedFile<T>(string aggregatedFileName, DataAggregation aggregation, REScan.MCode.Interpolator interpolator, DataIO<T> io, 
            string wptFileName, List<Waypoint> wptMeasurements, ProcessStats stats) where T : Measurement {
            var measFileName = FileUtility.FindValidFile(wptFileName, io.Extension());
            if(!String.IsNullOrEmpty(measFileName)) {
                var meta = new Meta(measFileName);
                var measList = io.ReadFile(measFileName);

                interpolator.Interpolate(ref measList, wptMeasurements);
                
                var interpolatedMeasList = measList.FindAll(meas => meas.IsInterpolated).ToList();

                switch(aggregation) {
                    case DataAggregation.Tech: {
                        var outputFileName = System.IO.Path.ChangeExtension(aggregatedFileName, io.REAnalysisExtension());
                        var append = OutputFileNames.Exists(x => x == outputFileName);
                        if(!append)
                            OutputFileNames.Add(outputFileName);
                        io.OutputRedeyeAnalysisFile(outputFileName, interpolatedMeasList, meta, append);
                        break;
                    }
                    case DataAggregation.TechHeight: {
                        interpolatedMeasList.Sort((meas1, meas2) => meas1.Height.CompareTo(meas2.Height));
                        var heightLists = interpolatedMeasList.GroupBy(meas => meas.Height).ToList();
                        foreach(var group in heightLists) {
                            var outputFileName = System.IO.Path.GetDirectoryName(aggregatedFileName) + "\\" +
                                System.IO.Path.GetFileNameWithoutExtension(aggregatedFileName) + "_height_" + group.First().Height.ToString();
                            outputFileName = outputFileName + "." + io.REAnalysisExtension();
                            var append = OutputFileNames.Exists(x => x == outputFileName);
                            if(!append)
                                OutputFileNames.Add(outputFileName);
                            io.OutputRedeyeAnalysisFile(outputFileName, group.ToList(), meta, append);
                        }
                        break;
                    }
                    case DataAggregation.None: {
                        var outputFileName = System.IO.Path.ChangeExtension(measFileName, io.REAnalysisExtension());
                        io.OutputRedeyeAnalysisFile(outputFileName, interpolatedMeasList, meta, false);
                        break;    
                    }                       
                };
            }
        }
        private void OutputReformattedFile<T>(string aggregatedFileName, DataAggregation aggregation, DataIO<T> io, string measFileName, ProcessStats stats) where T : Measurement {
            if(FileUtility.DoesExtensionMatch(measFileName, io.Extension())) {
                if(io.IsEmpty(measFileName)) {
                    Log("Found empty measurement file. Skipping... Filename: " + measFileName + ".");
                    return;
                }

                var meta = new Meta(measFileName);
                var measList = io.ReadFile(measFileName);
                var outputList = measList;
                if(!Convert.ToBoolean(IgnoreGpsLock.IsChecked))
                    outputList = measList.FindAll(meas => meas.IsGpsLocked).ToList();

                outputList.ForEach(meas => meas.Height = 0);

                switch(aggregation) {
                    case DataAggregation.Tech: {
                        var outputFileName = System.IO.Path.ChangeExtension(aggregatedFileName, io.REAnalysisExtension());
                        var append = OutputFileNames.Exists(x => x == outputFileName);
                        if(!append)
                            OutputFileNames.Add(outputFileName);
                        io.OutputRedeyeAnalysisFile(outputFileName, outputList, meta, append);
                        break;
                    }
                    case DataAggregation.TechHeight: {
                        outputList.Sort((meas1, meas2) => meas1.Height.CompareTo(meas2.Height));
                        var heightLists = outputList.GroupBy(meas => meas.Height).ToList();
                        foreach(var group in heightLists) {
                            var outputFileName = System.IO.Path.GetDirectoryName(aggregatedFileName) + "\\" +
                                System.IO.Path.GetFileNameWithoutExtension(aggregatedFileName) + "_height_" + group.First().Height.ToString();
                            outputFileName = System.IO.Path.ChangeExtension(outputFileName, io.REAnalysisExtension());
                            var append = OutputFileNames.Exists(x => x == outputFileName);
                            if(!append)
                                OutputFileNames.Add(outputFileName);
                            io.OutputRedeyeAnalysisFile(outputFileName, group.ToList(), meta, append);
                        }
                        break;
                    }
                    case DataAggregation.None: {
                        var outputFileName = System.IO.Path.ChangeExtension(measFileName, io.REAnalysisExtension());
                        io.OutputRedeyeAnalysisFile(outputFileName, outputList, meta, false);
                        break;
                    }
                };
            }
        }

        private void ExitButton_Click(object sender, RoutedEventArgs e) {
            this.Close();
        }

        private void ChooseReformatFilesButton_Click(object sender, RoutedEventArgs e) {
            var extension = "*.dmm;*.wnd;*.wnu;*.wnl";
            var filter = "All Measurement Files|*.dmm;*.wnd;*.wnu;*.wnl|All Files|*.*";
            SelectDataFiles(extension, filter, ReformatFilesListBox);
        }

        private string ChooseOutputFileButton_Click(object sender, RoutedEventArgs e) {
            string filename = "";

            try {
                // Create SaveFileDialog 
                Microsoft.Win32.SaveFileDialog dlg = new Microsoft.Win32.SaveFileDialog();

                // Set filter for file extension and default file extension 
                dlg.DefaultExt = ".NOT_USED";
                dlg.Filter = "All Files|*.*";


                // Display OpenFileDialog by calling ShowDialog method 
                Nullable<bool> result = dlg.ShowDialog();

                // Get the selected file name and display in a TextBox 
                if(result == true) {
                    // Open document 
                    filename = dlg.FileName;
                    //OutputFileTextBox.Text = filename;
                }
            }
            catch(Exception exc) {
                OnException(exc);
            }

            return filename;
        }

        private void HelpButton_Click(object sender, RoutedEventArgs e) {
            string msg = "MCode processes and reformats measurements so that they can be fed into RedEye for further analysis."
                + System.Environment.NewLine + System.Environment.NewLine +
                "Using waypoint files, MCode can interpolate the position of measurements.  It can also reformat measurements that " +
                "already have a valid position from GPS."
                + System.Environment.NewLine + System.Environment.NewLine +
                "For interpolation each group of measurements need a corresponding waypoint file with the same base filename.  For example: 'project_1__floor_1.dmm' needs a corresponding 'project_1__floor_1.wpt'.  " +
                "The filenames must exactly match excluding the file extension.";
            msg += System.Environment.NewLine + System.Environment.NewLine;
            msg += "(" + versioninfo.Content + ")";
            MessageBox.Show(this, msg, "MCode Interpolation Software", MessageBoxButton.OK, MessageBoxImage.Information);
        }

        private string GetProductVersion() {
            string version = "";

            try {
                System.Reflection.Assembly a = System.Reflection.Assembly.GetExecutingAssembly();
                {
                    System.Reflection.AssemblyName an = a.GetName();
                    version += an.Name;
                    version += " ";
                    version += an.Version;
                    version += "; ";
                }

                foreach(System.Reflection.AssemblyName an in a.GetReferencedAssemblies()) {
                    if(an.Name == "mcode") {
                        version += an.Name;
                        version += " library ";
                        version += an.Version;
                    }
                }
            }
            catch(Exception exc) {
                System.Diagnostics.Debug.WriteLine("GetProductVersion exc: " + exc.Message);
            }

            return version;
        }

        private void Window_Loaded(object sender, RoutedEventArgs e) {
            try {
                versioninfo.Content = GetProductVersion();
                //brushWarning = WaypointFileTextBox.Background;
                brushWarning = InterpolationFilesListBox.Background;
                brushOk = Brushes.White;
            }
            catch(Exception exc) {
                OnException(exc);
            }
        }

        private void RemoveReformatFilesButton_Click(object sender, RoutedEventArgs e) {
            RemoveReformatFilesFromList();
        }

        private void RemoveReformatFilesFromList() {
            try {
                int item_idx;
                while((item_idx = ReformatFilesListBox.SelectedIndex) >= 0) {
                    ReformatFilesListBox.Items.RemoveAt(item_idx);
                }
            }
            catch(Exception exc) {
                OnException(exc);
            }
        }

        private void ReformatFilesListBox_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Delete) {
                RemoveReformatFilesFromList();
            }
        }

        private void RemoveInterpolationFileButton_Click(object sender, RoutedEventArgs e) {
            RemoveInterpolationFilesFromList();
        }

        private void RemoveInterpolationFilesFromList() {
            try {
                int item_idx;
                while((item_idx = InterpolationFilesListBox.SelectedIndex) >= 0) {
                    InterpolationFilesListBox.Items.RemoveAt(item_idx);
                }
            }
            catch(Exception exc) {
                OnException(exc);
            }
        }

        private void InterpolationFilesListBox_KeyDown(object sender, KeyEventArgs e) {
            if(e.Key == Key.Delete) {
                RemoveInterpolationFilesFromList();
            }
        }

        private void IsAggregatedByTech_Checked(object sender, RoutedEventArgs e) {
            if(Convert.ToBoolean(IsAggregatedByTech.IsChecked))
                IsAggregatedByTechHeight.IsChecked = false;
        }

        private void IsAggregatedByTechHeight_Checked(object sender, RoutedEventArgs e) {
            if(Convert.ToBoolean(IsAggregatedByTechHeight.IsChecked))
                IsAggregatedByTech.IsChecked = false;

        }

    }
}
