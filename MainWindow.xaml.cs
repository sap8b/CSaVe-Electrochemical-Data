using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Data;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using CSaVe_Electrochemical_Data.Services;
using CSaVe_Electrochemical_Data.Services.Polarization.Reactions;
using OxyPlot;
using OxyPlot.Axes;
using OxyPlot.Legends;
using OxyPlot.Series;

namespace CSaVe_Electrochemical_Data
{
    /// <summary>
    /// Interaction logic for MainWindow.xaml
    /// </summary>
    public partial class MainWindow : Window
    {
        private enum TypeOfDTAFile
        {
            EISPOT, POTENTIODYNAMIC, CORPOT, COLLECT, CV, CYCPOL, GALVEIS, GALVANOSTATIC, POTENTIOSTATIC, UNK
        }
        private string ConvertDTAToString(TypeOfDTAFile val)
        {
            string output;

            switch (val)
            {
                case TypeOfDTAFile.COLLECT:
                    output = "COLLECT";
                    break;
                case TypeOfDTAFile.CYCPOL:
                    output = "CYCPOL";
                    break;
                case TypeOfDTAFile.CV:
                    output = "CV";
                    break;
                case TypeOfDTAFile.CORPOT:
                    output = "CORPOT";
                    break;
                case TypeOfDTAFile.EISPOT:
                    output = "EISPOT";
                    break;
                case TypeOfDTAFile.GALVEIS:
                    output = "GALVEIS";
                    break;
                case TypeOfDTAFile.GALVANOSTATIC:
                    output = "GALVANOSTATIC";
                    break;
                case TypeOfDTAFile.POTENTIODYNAMIC:
                    output = "POTENTIODYNAMIC";
                    break;
                case TypeOfDTAFile.POTENTIOSTATIC:
                    output = "POTENTIOSTATIC";
                    break;
                default:
                    output = "Type Unknown";
                    break;
            }

            return output;
        }

        private TypeOfDTAFile ConvertStringToDTA(string val)
        {
            TypeOfDTAFile output;

            switch (val)
            {
                case "EISPOT":
                    output = TypeOfDTAFile.EISPOT;
                    break;
                case "POTENTIODYNAMIC":
                    output = TypeOfDTAFile.POTENTIODYNAMIC;
                    break;
                case "CORPOT":
                    output = TypeOfDTAFile.CORPOT;
                    break;
                case "COLLECT":
                    output = TypeOfDTAFile.COLLECT;
                    break;
                case "CV":
                    output = TypeOfDTAFile.CV;
                    break;
                case "CYCPOL":
                    output = TypeOfDTAFile.CYCPOL;
                    break;
                case "GALVEIS":
                    output = TypeOfDTAFile.GALVEIS;
                    break;
                case "GALVANOSTATIC":
                    output = TypeOfDTAFile.GALVANOSTATIC;
                    break;
                case "POTENTIOSTATIC":
                    output = TypeOfDTAFile.POTENTIOSTATIC;
                    break;
                default:
                    output = TypeOfDTAFile.UNK;
                    break;

            }

            return output;
        }

        private FolderBrowserDialog SelectFolderForDTAFiles, SelectFolderForCSVFiles;
        private DirectoryInfo dataFolder;

        private List<FileInfo> list_of_DTA_files;
        private List<string> list_of_folders_in_DTA_directory; // = new List<string>();
        private List<string> list_of_folders_in_CSV_directory; // = new List<string>();

        // create a background worker
        private BackgroundWorker worker;

        //Message window that displays the progress bar
        MessageWindowDialog mwd;

        private readonly IPolarizationAnalysisService _polarizationAnalysisService;
        private string _anodicPolarizationFilePath = string.Empty;
        private string _cathodicPolarizationFilePath = string.Empty;
        private readonly PlotModel polarizationPlotModel = new();
        private readonly DataTable polarizationFitResultsTable = new();
        private readonly Dictionary<string, int> polarizationFitRowIndexByName = new(StringComparer.Ordinal);
        private int polarizationAnalysisRunCount;
        private static readonly Regex PdTokenRegex = new(@"(^|[^a-z])pd([^a-z]|$)", RegexOptions.Compiled);
        private static readonly string[] PolarizationFitParameterRows =
        {
            "Metal species",
            "Temperature (oC)",
            "pH",
            "Cl⁻ concentration (M)",
            "Metal ion concentration [Mz+] (M)",
            "I₀, metal (A/cm2)",
            "β_metal",
            "I₀, ORR (A/cm2)",
            "β_ORR",
            "i_lim, ORR (A/cm2)",
            "I₀, HER (A/cm2)",
            "β_HER",
            "E_corr (mV)",
            "i_corr (µA/cm2)",
            "i@-850 mV (µA/cm2)",
            "i@-1050 mV (µA/cm2)",
            "Weighted RMSE (A/cm2)"
        };
        private static readonly string[] PolarizationFittedOnlyRows =
        {
            "E_corr (mV)",
            "i_corr (µA/cm2)",
            "i@-850 mV (µA/cm2)",
            "i@-1050 mV (µA/cm2)",
            "Weighted RMSE (A/cm2)"
        };

        public MainWindow()
        {
            InitializeComponent();

            _polarizationAnalysisService = new PolarizationAnalysisService(
                new PolarizationCsvReader(),
                new MonotonicityFilter(),
                new PolarizationCurveJoiner(),
                new BvCurveFitter());

            AnodicCsvPath.TextChanged += (_, _) => UpdateXmlGenerationAvailability();
            CathodicCsvPath.TextChanged += (_, _) => UpdateXmlGenerationAvailability();
            UpdateXmlGenerationAvailability();

            InitializePlotModels();
            InitializePolarizationFitResultsTable();
            UpdateOrrIlimFromConditions();
        }
        /// <summary>
        /// This method searches through the provided folder and subfolders or just through the provided folder to find all DTA files and then uses the Background Worker to call
        /// the file conversion method on each file
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void ConvertFiles_Click(object sender, RoutedEventArgs e)
        {
            mwd = new MessageWindowDialog();
            StatusBox.Text = "Conversions not yet finsished.";

            //Initialize the dispatcher
            System.Windows.Threading.Dispatcher mwDispatcher = mwd.Dispatcher;

            //Initialize the background worker and support progress reporting
            worker = new BackgroundWorker
            {
                WorkerReportsProgress = true
            };


            // Check to see if both the DTA folders list has at least one entry in it and the CSV folders list has at least one entry in it
            if (list_of_folders_in_DTA_directory.Count > 0 && list_of_folders_in_CSV_directory.Count > 0)
            {
                worker.DoWork += delegate (object s, DoWorkEventArgs args)
                {
                    int maxNumFiles = 0;
                    int k = 0;

                    // Iterate through each folder name in the DTA folders list
                    foreach (string entry in list_of_folders_in_DTA_directory)
                    {
                        // Initialize each folder name as a directoryinfo object
                        DirectoryInfo dataFolder = new DirectoryInfo(entry);

                        // Search the directoryinfo folder for all dta files and add them to a FIleInfo array
                        FileInfo[] temp_list_files = dataFolder.GetFiles("*.dta");

                        // If there is at least one dta file in the folder, iterate through the array of fileinfo and add the names to a list of DTA names
                        if (temp_list_files.Length > 0)
                        {
                            list_of_DTA_files = new List<FileInfo>();
                            foreach (FileInfo lfile in temp_list_files)
                            {
                                list_of_DTA_files.Add(lfile);
                            }

                            // Create and initialize a DirectoryInfo object for the matching CSV folder name from the CSV folder list
                            DirectoryInfo copyFolder = new DirectoryInfo(list_of_folders_in_CSV_directory[k]);

                            // For each FIleInfo object in the list of dta files, create a StreamReader object, create the new csv filenmae, and pass these entries
                            // to the Conversion routine
                            maxNumFiles = list_of_DTA_files.Count;
                            int ii = 0;
                            foreach (FileInfo file in list_of_DTA_files)
                            {
                                StreamReader sR = new StreamReader(file.OpenRead());

                                string iname = file.Name;
                                string[] split_filename = iname.Split('.', StringSplitOptions.RemoveEmptyEntries);

                                string new_filename_base = split_filename[0].ToUpper();
                                string suffix = ".csv";

                                int result = ConvertGamryDatafileToCSVFile(copyFolder.FullName, new_filename_base, suffix, sR);

                                // Create and initialize a delegate to accept the progress bar update method
                                UpdateProgressDelegate update = new UpdateProgressDelegate(UpdateProgressBar);

                                // Pass the delegate and the value to the dispatcher
                                mwDispatcher.BeginInvoke(update, Convert.ToInt32(ii / (decimal)maxNumFiles * 100), maxNumFiles);
                                ii += 1;

                            }
                        }

                        k += 1;
                    }


                    // Background worker method for reporting progress
                    worker.ProgressChanged += delegate (object s, ProgressChangedEventArgs args)
                    {
                        int percentage = args.ProgressPercentage;

                    };

                };

                // Background worker method to report task complete
                worker.RunWorkerCompleted += delegate (object s, RunWorkerCompletedEventArgs args)
                {
                    StatusBox.Text = "Conversion Complete";
                    mwd.Close();
                };

                worker.RunWorkerAsync();
                mwd.ShowDialog();

            }
        }

        /// <summary>
        /// Delegate used for the method to update the Message Window's progress bar
        /// </summary>
        /// <param name="percentage"></param>
        /// <param name="recordCount"></param>
        public delegate void UpdateProgressDelegate(int percentage, int recordCount);

        /// <summary>
        /// Method for updating the Message Window's progress bar
        /// </summary>
        /// <param name="percentage"></param>
        /// <param name="recordCount"></param>        
        public void UpdateProgressBar(int percentage, int recordCount)
        {
            mwd.ProgressValue = percentage;
        }

        /// <summary>
        /// This method provides the folder browser dialog to permit designating the directory in which to store the converted datafiles
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void SelectOutputLocationButton_Click(object sender, RoutedEventArgs e)
        {
            list_of_folders_in_CSV_directory = new List<string>();
            SelectFolderForCSVFiles = new FolderBrowserDialog
            {
                RootFolder = Environment.SpecialFolder.MyComputer,
                UseDescriptionForTitle = true,
                Description = "Pick the parent directory to create the new CSV files in"
            };

            SelectFolderForCSVFiles.ShowDialog();
            string csv_directory = SelectFolderForCSVFiles.SelectedPath;

            if (csv_directory != null && csv_directory != "")
            {
                OutputFolder.Text = csv_directory;

                // This conditional statement checks the length of the dta folder list.
                // If the count > 0 then it creates copies of the folders
                if (list_of_folders_in_DTA_directory.Count >= 2)
                {
                    int i = 0;
                    foreach (string centry in list_of_folders_in_DTA_directory)
                    {
                        DirectoryInfo subFolder = new DirectoryInfo(centry);
                        string new_name = System.IO.Path.Combine(csv_directory, subFolder.Name);

                        if (!Directory.Exists(new_name))
                        {
                            Directory.CreateDirectory(new_name);
                            list_of_folders_in_CSV_directory.Add(new_name);
                            i += 1;
                        }
                        else
                        {
                            list_of_folders_in_CSV_directory.Add(new_name);
                            i += 1;
                        }
                    }
                    ConvertFiles.IsEnabled = true;
                }
                // If the count == 0, then it creates only a single folder that matches the folder name in the dta folder list
                else
                {
                    string new_name = csv_directory; // System.IO.Path.Combine(csv_directory, dataFolder.Name);
                    list_of_folders_in_CSV_directory.Add(new_name);

                    if (!Directory.Exists(new_name))
                    {
                        Directory.CreateDirectory(new_name);
                    }
                    if (list_of_DTA_files.Count > 0)
                    {
                        ConvertFiles.IsEnabled = true;
                    }
                    else
                    {
                        System.Windows.MessageBox.Show("There do not appear to be any DTA files in the selected directory.");
                    }
                }

            }
        }

        /// <summary>
        /// This method provides the folder browser dialog to designate the base folder where the dta files are located
        /// </summary>
        /// <param name="sender"></param>
        /// <param name="e"></param>
        private void LoadDataButton_Click(object sender, RoutedEventArgs e)
        {
            // Iniitialize critical settings
            DataFilesTextBox.Text = "Datafiles";
            StatusBox.Text = "Status";
            list_of_folders_in_DTA_directory = new List<string>();

            SelectFolderForDTAFiles = new FolderBrowserDialog
            {
                RootFolder = Environment.SpecialFolder.MyComputer,
                UseDescriptionForTitle = true,
                Description = "Pick the parent directory for the folders containing DTA datafiles."
            };

            SelectFolderForDTAFiles.ShowDialog();
            string base_directory = SelectFolderForDTAFiles.SelectedPath;

            if (Directory.Exists(base_directory)) //base_directory != null && base_directory != ""
            {
                // Update the ParentFolder textbox in the MainWindow
                ParentFolder.Text = base_directory;

                DirectoryInfo directory = new DirectoryInfo(base_directory);
                string[] temp_directory_array = Directory.GetDirectories(directory.FullName);

                // This conditional statement checks to see if multiple folders are present in the selected data folder
                // If there are multiple folders then they are added to the dta folder list
                if (temp_directory_array.Length > 0)
                {
                    DataFilesTextBox.Text = "";
                    foreach (string t_directory in temp_directory_array)
                    {
                        list_of_folders_in_DTA_directory.Add(t_directory);
                        DataFilesTextBox.Text += t_directory + Environment.NewLine;
                    }
                    SelectOutputLocationButton.IsEnabled = true;
                }
                // if there are not multiple folders then the base folder is assumed to contain all of the dta files.
                // The base folder is added to the dta folder list
                else
                {
                    dataFolder = new DirectoryInfo(base_directory);
                    list_of_folders_in_DTA_directory.Add(base_directory);

                    // The base folder is searched for dta files
                    FileInfo[] temp_list_files = dataFolder.GetFiles("*.dta");

                    // If any are found, they are added to the list of dta files
                    if (temp_list_files.Length > 0)
                    {
                        list_of_DTA_files = new List<FileInfo>();
                        ; foreach (FileInfo t_directory in temp_list_files)
                        {
                            list_of_DTA_files.Add(t_directory);
                            DataFilesTextBox.Text += t_directory + Environment.NewLine;
                        }
                        SelectOutputLocationButton.IsEnabled = true;
                    }
                    // If none are found, then an error message is displayed
                    else
                    {
                        System.Windows.MessageBox.Show("There do not appear to be any DTA files in this directory.");
                    }
                }
            }
            // If no data directory is chosen, an error message is displayed
            else
            {
                System.Windows.MessageBox.Show("No directory selected.");
            }
        }

        /// <summary>
        /// This method extracts the experimental data from the dta file and copies it to the csv file
        /// </summary>
        /// <param name="copy_filename"></param>
        /// <param name="sR"></param>
        /// <returns></returns>
        private int ConvertGamryDatafileToCSVFile(string copy_folder, string copy_filename_base, string suffix, StreamReader sR)
        {
            int result = 0;
            

            _ = sR.ReadLine();
            string line1 = sR.ReadLine();

            string[] line1_split = line1.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

            if (line1_split.Length > 0)
            {
                string read_a_line, element0;

                bool keep_header_line = false;
                bool keep_data_lines = false;
                bool keep_header_line2 = false;
                bool keep_data_lines2 = false;

                int line_count = 0;
                int line_count2 = 0;
                result = 1;
                FileStream fS, fS1, fS2;
                StreamWriter sW, sW1, sW2;

                TypeOfDTAFile file_type = ConvertStringToDTA(line1_split[1].ToUpper());
                string top_data_copy_filename, middle_data_copy_filename, bottom_data_copy_filename;
                string stop_element0, stop_element1, stop_element2, stop_element3;

                switch (file_type)
                {
                    case TypeOfDTAFile.EISPOT:
                        // Outputs 1 datafile with impedance data
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_eispot" + suffix);
                        stop_element0 = "ZCURVE";
                        Output1DataFile(top_data_copy_filename, stop_element0, sR);
                        break;
                    case TypeOfDTAFile.POTENTIODYNAMIC:
                        // Outputs 2 datafiles: 1 with Eoc and 1 with potentiodynamic data
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_eoc" + suffix);
                        bottom_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_pd" + suffix);
                        stop_element0 = "OCVCURVE";
                        stop_element1 = "EOC";
                        stop_element2 = "CURVE";
                        Output2DataFiles(top_data_copy_filename, bottom_data_copy_filename, stop_element0, stop_element1, stop_element2, sR);
                        break;
                    case TypeOfDTAFile.CORPOT:
                        // Outputs 1 datafile with corrosion potential data
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_corpot" + suffix);
                        stop_element0 = "CURVE";
                        Output1DataFile(top_data_copy_filename, stop_element0, sR);
                        break;
                    case TypeOfDTAFile.COLLECT:
                        // Outputs 3 datafiles: 1 with Eoc, 1 with ring data, and 1 with disc data
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_eoc" + suffix);
                        middle_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_ring" + suffix);
                        bottom_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_disk" + suffix);
                        stop_element0 = "OCVCURVE";
                        stop_element1 = "PSTATMODEL";
                        stop_element2 = "RINGCURVE";
                        stop_element3 = "DISKCURVE";
                        Output3DataFiles(top_data_copy_filename, middle_data_copy_filename, bottom_data_copy_filename, stop_element0, stop_element1, stop_element2, stop_element3, sR);
                        break;
                    case TypeOfDTAFile.CV:
                        // Outputs 2 datafiles: 1 with Eoc and 1 with all CV curve data
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_eoc" + suffix);
                        bottom_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_cv" + suffix);
                        stop_element0 = "OCVCURVE";
                        stop_element1 = "EOC";
                        stop_element2 = "CURVE1";
                        Output2DataFiles_CV(top_data_copy_filename, bottom_data_copy_filename, stop_element0, stop_element1, stop_element2, sR);
                        break;
                    case TypeOfDTAFile.CYCPOL:
                        // Outputs 2 datafiles: 1 with Eoc and 1 with cyclic polarization data
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_eoc" + suffix);
                        bottom_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_cycpol" + suffix);
                        stop_element0 = "OCVCURVE";
                        stop_element1 = "EOC";
                        stop_element2 = "CURVE";
                        Output2DataFiles(top_data_copy_filename, bottom_data_copy_filename, stop_element0, stop_element1, stop_element2, sR);
                        break;
                    case TypeOfDTAFile.GALVEIS:
                        // Outputs 1 datafile with impedance data
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_eisgalv" + suffix);
                        stop_element0 = "ZCURVE";
                        Output1DataFile(top_data_copy_filename, stop_element0, sR);
                        break;
                    case TypeOfDTAFile.GALVANOSTATIC:
                        // Outputs 2 datafiles: 1 with Eoc and 1 with galvanostatic data
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_eoc" + suffix);
                        bottom_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_galv" + suffix);
                        stop_element0 = "OCVCURVE";
                        stop_element1 = "EOC";
                        stop_element2 = "CURVE";
                        Output2DataFiles(top_data_copy_filename, bottom_data_copy_filename, stop_element0, stop_element1, stop_element2, sR);
                        break;
                    case TypeOfDTAFile.POTENTIOSTATIC:
                        // Outputs 2 datafiles: 1 with Eoc and 1 with potentiostatic data
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_eoc" + suffix);
                        bottom_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_ps" + suffix);
                        stop_element0 = "OCVCURVE";
                        stop_element1 = "EOC";
                        stop_element2 = "CURVE";
                        Output2DataFiles(top_data_copy_filename, bottom_data_copy_filename, stop_element0, stop_element1, stop_element2, sR);
                        result = 1;
                        break;
                    default:
                        top_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_eoc" + suffix);
                        bottom_data_copy_filename = System.IO.Path.Combine(copy_folder, copy_filename_base + "_pd" + suffix);
                        break;
                }
            }

            return result;
        }

        private static void Output1DataFile(string top_data_copy_filename, string stop_element0, StreamReader sR)
        {
            FileStream fS;
            StreamWriter sW;

            string read_a_line, element0;

            bool keep_header_line = false;
            bool keep_data_lines = false;

            int line_count = 0;

            fS = new FileStream(top_data_copy_filename, FileMode.Create);
            sW = new StreamWriter(fS);

            while ((read_a_line = sR.ReadLine()) != null)
            {
                string[] line_array = read_a_line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (line_array.Length > 0)
                {
                    element0 = line_array[0];
                    if (keep_header_line == false && keep_data_lines == true)
                    {
                        int point_check = Convert.ToInt32(line_array[0]);

                        // This checks for missing data points in EIS files and adds the previous value if one is missing
                        if (line_count != point_check)
                        {
                            sW.WriteLine(Make_a_CSV_Line(line_array));
                            line_count += 1;
                        }

                        sW.WriteLine(Make_a_CSV_Line(line_array));
                        line_count += 1;
                    }
                    else if (element0 == stop_element0 && keep_header_line == false)
                    {
                        keep_header_line = true;
                    }
                    else if (keep_header_line == true)
                    {
                        sW.WriteLine(Make_a_CSV_Line(line_array));

                        // Skip the units line
                        _ = sR.ReadLine();

                        keep_header_line = false;
                        keep_data_lines = true;
                    }
                }
            }
            sW.Close();
            fS.Close();
        }

        private static void Output2DataFiles(string top_data_copy_filename, string bottom_data_copy_filename,  string stop_element0, string stop_element1, string stop_element2,
            StreamReader sR)
        {
            FileStream fS, fS1;
            StreamWriter sW, sW1;
            string read_a_line, element0;

            bool keep_header_line = false;
            bool keep_data_lines = false;
            bool keep_header_line2 = false;
            bool keep_data_lines2 = false;

            int line_count = 0;
            int line_count2 = 0;

            fS = new FileStream(top_data_copy_filename, FileMode.Create);
            fS1 = new FileStream(bottom_data_copy_filename, FileMode.Create);
            sW = new StreamWriter(fS);
            sW1 = new StreamWriter(fS1);

            string eoc_readline;

            while ((eoc_readline = sR.ReadLine()) != null)
            {
                string[] line_array = eoc_readline.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (line_array.Length > 0)
                {
                     element0 = line_array[0];

                    if (element0 == stop_element1 && keep_data_lines == true)
                    {
                        keep_data_lines = false;
                        keep_header_line = false;
                    }

                    if (keep_header_line == false && keep_data_lines == true)
                    {
                        // Get Eoc data
                        int point_check = Convert.ToInt32(line_array[0]);

                        if (line_count != point_check)
                        {
                            sW.WriteLine(Make_a_CSV_Line(line_array));
                            line_count += 1;
                        }

                        sW.WriteLine(Make_a_CSV_Line(line_array));
                        line_count += 1;
                    }

                    if (keep_header_line2 == false && keep_data_lines2 == true)
                    {
                        // Get potentiodynmic data
                        int point_check = Convert.ToInt32(line_array[0]);

                        if (line_count2 != point_check)
                        {
                            sW1.WriteLine(Make_a_CSV_Line(line_array));
                            line_count2 += 1;
                        }

                        sW1.WriteLine(Make_a_CSV_Line(line_array));
                        line_count2 += 1;
                    }

                    if (element0 == stop_element0 && keep_header_line == false)
                    {
                        keep_header_line = true;
                        // Get Eoc header information
                        string next_line = sR.ReadLine();
                        string[] nline_split = next_line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        sW.WriteLine(Make_a_CSV_Line(nline_split));
                        _ = sR.ReadLine();
                        keep_header_line = false;
                        keep_data_lines = true;
                    }

                    if (element0 == stop_element2 && keep_header_line2 == false)
                    {
                        keep_header_line2 = true;
                        // Get Potentiodynamic header informatiom
                        string next_line = sR.ReadLine();
                        string[] nline_split = next_line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        sW1.WriteLine(Make_a_CSV_Line(nline_split));
                        _ = sR.ReadLine();
                        keep_header_line2 = false;
                        keep_data_lines2 = true;
                    }
                }
            }

            sW1.Close();
            sW.Close();
            fS1.Close();
            fS.Close();
        }

        private static void Output3DataFiles(string top_data_copy_filename, string middle_data_copy_filename, string bottom_data_copy_filename,
            string stop_element0, string stop_element1, string stop_element2, string stop_element3, StreamReader sR)
        {
            FileStream fS, fS1, fS2;
            StreamWriter sW, sW1, sW2;
            string read_a_line, element0;

            bool keep_header_line = false;
            bool keep_data_lines = false;

            bool keep_header_line2 = false;
            bool keep_data_lines2 = false;

            bool keep_header_line3 = false;
            bool keep_data_lines3 = false;

            int line_count = 0;
            int line_count2 = 0;
            int line_count3 = 0;

            fS = new FileStream(top_data_copy_filename, FileMode.Create);
            fS1 = new FileStream(middle_data_copy_filename, FileMode.Create);
            fS2 = new FileStream(bottom_data_copy_filename, FileMode.Create);
            sW = new StreamWriter(fS);
            sW1 = new StreamWriter(fS1);
            sW2 = new StreamWriter(fS2);

            string eoc_readline;

            while ((eoc_readline = sR.ReadLine()) != null)
            {
                string[] line_array = eoc_readline.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (line_array.Length > 0)
                {
                    element0 = line_array[0];

                    if (element0 == stop_element1 && keep_data_lines == true)
                    {
                        keep_data_lines = false;
                        keep_header_line = false;
                    }
                    if (element0 == stop_element3 && keep_data_lines2 == true) 
                    {
                        keep_data_lines2 = false;
                        keep_header_line2 = false;
                    }

                    // Get Eoc data
                    if (keep_header_line == false && keep_data_lines == true)
                    {
                        // Get Eoc data
                        int point_check = Convert.ToInt32(line_array[0]);

                        if (line_count != point_check)
                        {
                            sW.WriteLine(Make_a_CSV_Line(line_array));
                            line_count += 1;
                        }

                        sW.WriteLine(Make_a_CSV_Line(line_array));
                        line_count += 1;
                    }

                    // Get ring data
                    if (keep_header_line2 == false && keep_data_lines2 == true)
                    {
                        // Get ring data
                        int point_check = Convert.ToInt32(line_array[0]);

                        if (line_count2 != point_check)
                        {
                            sW1.WriteLine(Make_a_CSV_Line(line_array));
                            line_count2 += 1;
                        }

                        sW1.WriteLine(Make_a_CSV_Line(line_array));
                        line_count2 += 1;
                    }

                    // Get disk data
                    if (keep_header_line3 == false && keep_data_lines3 == true) 
                    {
                        int point_check = Convert.ToInt32(line_array[0]);

                        if (line_count3 != point_check)
                        {
                            sW2.WriteLine(Make_a_CSV_Line(line_array));
                            line_count3 += 1;
                        }

                        sW2.WriteLine(Make_a_CSV_Line(line_array));
                        line_count3 += 1;
                    }

                    // Get Eoc header information
                    if (element0 == stop_element0 && keep_header_line == false)
                    {
                        keep_header_line = true;                      
                        string next_line = sR.ReadLine();
                        string[] nline_split = next_line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        sW.WriteLine(Make_a_CSV_Line(nline_split));
                        _ = sR.ReadLine();
                        keep_header_line = false;
                        keep_data_lines = true;
                    }

                    // Get ring header informatiom
                    if (element0 == stop_element2 && keep_header_line2 == false)
                    {
                        keep_header_line2 = true;                        
                        string next_line = sR.ReadLine();
                        string[] nline_split = next_line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        sW1.WriteLine(Make_a_CSV_Line(nline_split));
                        _ = sR.ReadLine();
                        keep_header_line2 = false;
                        keep_data_lines2 = true;
                    }

                    // Get disk header informatiom
                    if (element0 == stop_element3 && keep_header_line3 == false)
                    {
                        keep_header_line3 = true;
                        string next_line = sR.ReadLine();
                        string[] nline_split = next_line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        sW2.WriteLine(Make_a_CSV_Line(nline_split));
                        _ = sR.ReadLine();
                        keep_header_line3 = false;
                        keep_data_lines3 = true;
                    }

                }
            }
            sW2.Close();
            sW1.Close();
            sW.Close();
            fS2.Close();
            fS1.Close();
            fS.Close();
        }

        private static void Output2DataFiles_CV(string top_data_copy_filename, string bottom_data_copy_filename, string stop_element0, string stop_element1, string stop_element2,
    StreamReader sR)
        {
            FileStream fS, fS1;
            StreamWriter sW, sW1;
            string read_a_line, element0;

            bool keep_header_line = false;
            bool keep_data_lines = false;
            bool keep_header_line2 = false;
            bool keep_data_lines2 = false;

            int line_count = 0;
            int line_count2 = 0;

            fS = new FileStream(top_data_copy_filename, FileMode.Create);
            fS1 = new FileStream(bottom_data_copy_filename, FileMode.Create);
            sW = new StreamWriter(fS);
            sW1 = new StreamWriter(fS1);

            string eoc_readline;

            while ((eoc_readline = sR.ReadLine()) != null)
            {
                string[] line_array = eoc_readline.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);

                if (line_array.Length > 0)
                {
                    element0 = line_array[0];

                    if (element0 == stop_element1 && keep_data_lines == true)
                    {
                        keep_data_lines = false;
                        keep_header_line = false;
                    }

                    // Get Eoc data
                    if (keep_header_line == false && keep_data_lines == true)
                    {  
                        int point_check = Convert.ToInt32(line_array[0]);

                        if (line_count != point_check)
                        {
                            sW.WriteLine(Make_a_CSV_Line(line_array));
                            line_count += 1;
                        }

                        sW.WriteLine(Make_a_CSV_Line(line_array));
                        line_count += 1;
                    }

                    // Get CV data
                    if (keep_header_line2 == false && keep_data_lines2 == true)
                    {   
                        if (line_array[1] != "TABLE" && 
                            line_array[0] != "Pt" &&
                            line_array[0] != "#")
                        {
                            int point_check = Convert.ToInt32(line_array[0]);

                            if (line_count2 != point_check)
                            {
                                sW1.WriteLine(Make_a_CSV_Line(line_array));
                                line_count2 += 1;
                            }

                            sW1.WriteLine(Make_a_CSV_Line(line_array));
                            line_count2 += 1;
                        }
                        
                    }

                    // Get Eoc header information
                    if (element0 == stop_element0 && keep_header_line == false)
                    {
                        keep_header_line = true;                        
                        string next_line = sR.ReadLine();
                        string[] nline_split = next_line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        sW.WriteLine(Make_a_CSV_Line(nline_split));
                        _ = sR.ReadLine();
                        keep_header_line = false;
                        keep_data_lines = true;
                    }

                    // Get CV header informatiom
                    if (element0 == stop_element2 && keep_header_line2 == false)
                    {
                        keep_header_line2 = true;
                        string next_line = sR.ReadLine();
                        string[] nline_split = next_line.Split(new char[] { '\t' }, StringSplitOptions.RemoveEmptyEntries);
                        sW1.WriteLine(Make_a_CSV_Line(nline_split));
                        _ = sR.ReadLine();
                        keep_header_line2 = false;
                        keep_data_lines2 = true;
                    }
                }
            }

            sW1.Close();
            sW.Close();
            fS1.Close();
            fS.Close();
        }
        private static string Make_a_CSV_Line(string[] line_array)
        {
            string the_line2 = null;
            for (int i = 0; i <= line_array.Length - 2; i++)
            {
                the_line2 += line_array[i] + ",";
            }
            the_line2 += line_array[^1];

            return the_line2;
        }

        private enum XmlEligibleCsvType
        {
            CYCPOL,
            POTENTIODYNAMIC,
            POLARIZATION_UNKNOWN,
            UNSUPPORTED
        }

        private void InitializePlotModels()
        {
            polarizationPlotModel.Title = "Polarization (|i| vs E)";
            polarizationPlotModel.Axes.Add(new LogarithmicAxis { Position = AxisPosition.Bottom, Title = "Current Density (A/cm2)", Minimum = 1.0e-12 });
            polarizationPlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Potential (V)" });
            polarizationPlotModel.Legends.Add(new Legend
            {
                LegendPosition        = LegendPosition.BottomLeft,
                LegendPlacement       = LegendPlacement.Inside,
                LegendBackground      = OxyColor.FromAColor(200, OxyColors.White),
                LegendBorder          = OxyColors.Gray,
                LegendBorderThickness = 0.5,
                LegendFontSize        = 10.0,
            });
            PolarizationPlotView.Model = polarizationPlotModel;
        }

        private void InitializePolarizationFitResultsTable()
        {
            polarizationFitResultsTable.Columns.Clear();
            polarizationFitResultsTable.Rows.Clear();
            polarizationFitRowIndexByName.Clear();
            polarizationAnalysisRunCount = 0;
            polarizationFitResultsTable.Columns.Add("Parameter", typeof(string));

            foreach (string rowName in PolarizationFitParameterRows)
            {
                int rowIndex = polarizationFitResultsTable.Rows.Count;
                polarizationFitResultsTable.Rows.Add(rowName);
                polarizationFitRowIndexByName[rowName] = rowIndex;
            }

            PolarizationResultsGrid.ItemsSource = polarizationFitResultsTable.DefaultView;
        }

        private static XmlEligibleCsvType DetectXmlEligibleType(string csvPath)
        {
            if (string.IsNullOrWhiteSpace(csvPath) || !File.Exists(csvPath))
                return XmlEligibleCsvType.UNSUPPORTED;

            string fileName = Path.GetFileNameWithoutExtension(csvPath).ToLowerInvariant();
            if (fileName.Contains("cycpol"))
                return XmlEligibleCsvType.CYCPOL;
            if (fileName.Contains("potentiodynamic") || PdTokenRegex.IsMatch(fileName))
                return XmlEligibleCsvType.POTENTIODYNAMIC;

            string? header = File.ReadLines(csvPath).FirstOrDefault();
            if (string.IsNullOrWhiteSpace(header))
                return XmlEligibleCsvType.UNSUPPORTED;

            string[] cols = [.. header.Split(',').Select(c => c.Trim().ToLowerInvariant())];
            bool hasVf = cols.Contains("vf");
            bool hasIm = cols.Contains("im");
            if (hasVf && hasIm)
                return XmlEligibleCsvType.POLARIZATION_UNKNOWN;

            return XmlEligibleCsvType.UNSUPPORTED;
        }

        private void UpdateXmlGenerationAvailability()
        {
            string anodicPath = AnodicCsvPath.Text?.Trim() ?? string.Empty;
            string cathodicPath = CathodicCsvPath.Text?.Trim() ?? string.Empty;

            if (string.IsNullOrWhiteSpace(anodicPath))
            {
                GenerateXmlButton.IsEnabled = false;
                GenerateXmlButton.ToolTip = "Select a CYCPOL or POTENTIODYNAMIC-derived CSV file.";
                XmlStatusBox.Text = "Select a polarization CSV to enable XML generation.";
                return;
            }

            if (!File.Exists(anodicPath) || (!string.IsNullOrWhiteSpace(cathodicPath) && !File.Exists(cathodicPath)))
            {
                GenerateXmlButton.IsEnabled = false;
                GenerateXmlButton.ToolTip = "Selected file path does not exist.";
                XmlStatusBox.Text = "One or more selected files were not found.";
                return;
            }

            XmlEligibleCsvType anodicType = DetectXmlEligibleType(anodicPath);
            XmlEligibleCsvType cathodicType = string.IsNullOrWhiteSpace(cathodicPath)
                ? anodicType
                : DetectXmlEligibleType(cathodicPath);

            bool anodicOk = anodicType is XmlEligibleCsvType.CYCPOL or XmlEligibleCsvType.POTENTIODYNAMIC;
            bool cathodicOk = cathodicType is XmlEligibleCsvType.CYCPOL or XmlEligibleCsvType.POTENTIODYNAMIC;

            if (anodicOk && cathodicOk)
            {
                GenerateXmlButton.IsEnabled = true;
                GenerateXmlButton.ToolTip = "Generate XML from polarization CSV data.";
                XmlStatusBox.Text = "Ready: CYCPOL/POTENTIODYNAMIC CSV detected.";
            }
            else
            {
                GenerateXmlButton.IsEnabled = false;
                GenerateXmlButton.ToolTip = "XML generation supports only CYCPOL or POTENTIODYNAMIC-derived CSV files.";
                XmlStatusBox.Text = "XML generation disabled: selected CSV is not identified as CYCPOL/POTENTIODYNAMIC-derived.";
            }
        }

        private static string FormatFixed(double value, string format)
        {
            return double.IsFinite(value)
                ? value.ToString(format, CultureInfo.InvariantCulture)
                : "n/a";
        }

        /// <summary>
        /// Computes the ORR limiting current density from the current UI values of temperature,
        /// Cl⁻ concentration, and diffusion-layer thickness, and populates
        /// <see cref="OrrIlimTextBox"/>. Ignores invalid inputs without showing a dialog.
        /// </summary>
        private void UpdateOrrIlimFromConditions()
        {
            if (!TryParseIlimInputs(out double tempC, out double clM, out double deltaMicrons, showErrors: false))
                return;

            try
            {
                double deltaCm = deltaMicrons * 1.0e-4;
                double ilim = DissolvedOxygenCalculator.CalcOrrIlimAcm2(tempC, clM, deltaCm);
                OrrIlimTextBox.Text = ilim.ToString("E3", CultureInfo.InvariantCulture);
            }
            catch
            {
                // Silently ignore calculation errors during startup or background updates.
            }
        }

        /// <summary>
        /// Parses the temperature, Cl⁻ concentration, and diffusion-layer thickness from the UI.
        /// </summary>
        /// <param name="tempC">Parsed temperature (oC).</param>
        /// <param name="clM">Parsed Cl⁻ concentration (mol/L).</param>
        /// <param name="deltaMicrons">Parsed diffusion-layer thickness (µm).</param>
        /// <param name="showErrors">When true, shows a MessageBox for the first invalid input encountered.</param>
        /// <returns>True when all three inputs are valid.</returns>
        private bool TryParseIlimInputs(out double tempC, out double clM, out double deltaMicrons, bool showErrors)
        {
            tempC = 0; clM = 0; deltaMicrons = 0;

            if (!double.TryParse(TC.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out tempC)
                || !double.IsFinite(tempC))
            {
                if (showErrors)
                    System.Windows.MessageBox.Show("Enter a valid temperature (oC) before calculating i_lim.",
                        "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!double.TryParse(ClConc.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out clM)
                || clM < 0.0)
            {
                if (showErrors)
                    System.Windows.MessageBox.Show("Enter a valid Cl⁻ concentration (M) before calculating i_lim.",
                        "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            if (!double.TryParse(DiffLayerThicknessTextBox.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out deltaMicrons)
                || deltaMicrons <= 0.0)
            {
                if (showErrors)
                    System.Windows.MessageBox.Show("Enter a valid positive diffusion-layer thickness (µm) before calculating i_lim.",
                        "Input Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                return false;
            }

            return true;
        }

        private static string FormatScientific(double value)
        {
            return double.IsFinite(value)
                ? value.ToString("E3", CultureInfo.InvariantCulture)
                : "n/a";
        }

        private static string ParseUiOrAuto(string text, bool included)
        {
            if (!included)
                return "excluded";
            string trimmed = text?.Trim();
            return string.IsNullOrWhiteSpace(trimmed) ? "auto" : trimmed;
        }

        private static string QuoteCsvValue(string value)
        {
            return $"\"{(value ?? string.Empty).Replace("\"", "\"\"")}\"";
        }

        private static string UnquoteCsvValue(string line)
        {
            string trimmed = line?.Trim() ?? string.Empty;
            if (trimmed.Length >= 2 && trimmed[0] == '\"' && trimmed[^1] == '\"')
                return trimmed[1..^1].Replace("\"\"", "\"");
            return trimmed;
        }

        private static string GetImportedValue(IDictionary<string, string> values, string key)
        {
            return values.TryGetValue(key, out string value) ? value : string.Empty;
        }

        private static bool IsExportedMissingValue(string value)
        {
            return string.IsNullOrWhiteSpace(value)
                || string.Equals(value, "—", StringComparison.Ordinal)
                || string.Equals(value, "n/a", StringComparison.OrdinalIgnoreCase)
                || string.Equals(value, "auto", StringComparison.OrdinalIgnoreCase);
        }

        private static bool IsExcludedValue(string value)
        {
            return string.Equals(value?.Trim(), "excluded", StringComparison.OrdinalIgnoreCase);
        }

        private static string SelectImportedValue(string staticValue, string fitValue)
        {
            if (!IsExportedMissingValue(fitValue) && !IsExcludedValue(fitValue))
                return fitValue;
            if (!IsExportedMissingValue(staticValue) && !IsExcludedValue(staticValue))
                return staticValue;
            return string.Empty;
        }

        private MetalSpecies GetSelectedMetalSpecies()
        {
            string selected = (MetalSpeciesComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "Fe";
            return Enum.TryParse(selected, ignoreCase: true, out MetalSpecies metalSpecies)
                ? metalSpecies
                : MetalSpecies.Fe;
        }

        private string GetSelectedMetalSpeciesLabel()
        {
            return GetSelectedMetalSpecies().ToString();
        }

        private void SetSelectedMetalSpecies(string label)
        {
            if (string.IsNullOrWhiteSpace(label))
                return;

            foreach (object item in MetalSpeciesComboBox.Items)
            {
                if (item is ComboBoxItem comboBoxItem
                    && string.Equals(comboBoxItem.Content?.ToString(), label.Trim(), StringComparison.OrdinalIgnoreCase))
                {
                    MetalSpeciesComboBox.SelectedItem = comboBoxItem;
                    break;
                }
            }
        }

        private void ApplyImportedBvSetup(string parameterName, string staticValue, string fitValue)
        {
            string preferredValue = SelectImportedValue(staticValue, fitValue);
            switch (parameterName)
            {
                case "Metal species":
                    SetSelectedMetalSpecies(preferredValue);
                    break;
                case "Temperature (oC)":
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        TC.Text = preferredValue;
                    break;
                case "pH":
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        pH.Text = preferredValue;
                    break;
                case "Cl⁻ concentration (M)":
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        ClConc.Text = preferredValue;
                    break;
                case "Metal ion concentration [Mz+] (M)":
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        MetalIonConcTextBox.Text = preferredValue;
                    break;
                case "I₀, metal (A/cm2)":
                    MeOxIncludeCheckBox.IsChecked = !IsExcludedValue(fitValue) && !IsExcludedValue(staticValue);
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        MetalI0TextBox.Text = preferredValue;
                    break;
                case "β_metal":
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        MetalBetaTextBox.Text = preferredValue;
                    break;
                case "I₀, ORR (A/cm2)":
                    OrrIncludeCheckBox.IsChecked = !IsExcludedValue(fitValue) && !IsExcludedValue(staticValue);
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        OrrI0TextBox.Text = preferredValue;
                    break;
                case "β_ORR":
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        OrrBetaTextBox.Text = preferredValue;
                    break;
                case "i_lim, ORR (A/cm2)":
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        OrrIlimTextBox.Text = preferredValue;
                    break;
                case "I₀, HER (A/cm2)":
                    HerIncludeCheckBox.IsChecked = !IsExcludedValue(fitValue) && !IsExcludedValue(staticValue);
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        HerI0TextBox.Text = preferredValue;
                    break;
                case "β_HER":
                    if (!string.IsNullOrWhiteSpace(preferredValue))
                        HerBetaTextBox.Text = preferredValue;
                    break;
            }
        }

        private void SetPolarizationFitCell(string parameterName, string columnName, string value)
        {
            if (!polarizationFitRowIndexByName.TryGetValue(parameterName, out int rowIndex))
                throw new ArgumentException($"Unknown polarization fit table parameter row: '{parameterName}'.", nameof(parameterName));
            if (!polarizationFitResultsTable.Columns.Contains(columnName))
                throw new ArgumentException($"Unknown polarization fit table column: '{columnName}'.", nameof(columnName));

            DataRow row = polarizationFitResultsTable.Rows[rowIndex];
            row[columnName] = value;
        }

        private void AppendPolarizationFitResults(
            PolarizationAnalysisResult result,
            double iAt850Acm2,
            double iAt1050Acm2,
            double electrolyteTemperatureC,
            double electrolytePh,
            double chlorideConcentrationM,
            double metalIonConcentrationM)
        {
            polarizationAnalysisRunCount++;
            string staticColumnName = $"Static {polarizationAnalysisRunCount}";
            string fitColumnName = $"Fit {polarizationAnalysisRunCount}";
            polarizationFitResultsTable.Columns.Add(staticColumnName, typeof(string));
            polarizationFitResultsTable.Columns.Add(fitColumnName, typeof(string));

            bool includeMetal = MeOxIncludeCheckBox.IsChecked != false;
            bool includeOrr = OrrIncludeCheckBox.IsChecked != false;
            bool includeHer = HerIncludeCheckBox.IsChecked != false;
            BvModelParameters fp = result.FittedParameters;

            SetPolarizationFitCell("Metal species", staticColumnName, GetSelectedMetalSpeciesLabel());
            SetPolarizationFitCell("Metal species", fitColumnName, GetSelectedMetalSpeciesLabel());
            SetPolarizationFitCell("Temperature (oC)", staticColumnName, FormatFixed(electrolyteTemperatureC, "F3"));
            SetPolarizationFitCell("Temperature (oC)", fitColumnName, "—");
            SetPolarizationFitCell("pH", staticColumnName, FormatFixed(electrolytePh, "F3"));
            SetPolarizationFitCell("pH", fitColumnName, "—");
            SetPolarizationFitCell("Cl⁻ concentration (M)", staticColumnName, FormatScientific(chlorideConcentrationM));
            SetPolarizationFitCell("Cl⁻ concentration (M)", fitColumnName, "—");
            SetPolarizationFitCell("Metal ion concentration [Mz+] (M)", staticColumnName, FormatScientific(metalIonConcentrationM));
            SetPolarizationFitCell("Metal ion concentration [Mz+] (M)", fitColumnName, "—");

            SetPolarizationFitCell("I₀, metal (A/cm2)", staticColumnName, ParseUiOrAuto(MetalI0TextBox.Text, includeMetal));
            SetPolarizationFitCell("β_metal", staticColumnName, ParseUiOrAuto(MetalBetaTextBox.Text, includeMetal));
            SetPolarizationFitCell("I₀, ORR (A/cm2)", staticColumnName, ParseUiOrAuto(OrrI0TextBox.Text, includeOrr));
            SetPolarizationFitCell("β_ORR", staticColumnName, ParseUiOrAuto(OrrBetaTextBox.Text, includeOrr));
            SetPolarizationFitCell("i_lim, ORR (A/cm2)", staticColumnName, ParseUiOrAuto(OrrIlimTextBox.Text, includeOrr));
            SetPolarizationFitCell("I₀, HER (A/cm2)", staticColumnName, ParseUiOrAuto(HerI0TextBox.Text, includeHer));
            SetPolarizationFitCell("β_HER", staticColumnName, ParseUiOrAuto(HerBetaTextBox.Text, includeHer));

            SetPolarizationFitCell("I₀, metal (A/cm2)", fitColumnName, fp.IncludeMetal ? FormatScientific(fp.I0Metal) : "excluded");
            SetPolarizationFitCell("β_metal", fitColumnName, fp.IncludeMetal ? FormatFixed(fp.BetaMetal, "F4") : "excluded");
            SetPolarizationFitCell("I₀, ORR (A/cm2)", fitColumnName, fp.IncludeOrr ? FormatScientific(fp.I0Orr) : "excluded");
            SetPolarizationFitCell("β_ORR", fitColumnName, fp.IncludeOrr ? FormatFixed(fp.BetaOrr, "F4") : "excluded");
            SetPolarizationFitCell("i_lim, ORR (A/cm2)", fitColumnName, fp.IncludeOrr ? FormatScientific(fp.IlimOrr) : "excluded");
            SetPolarizationFitCell("I₀, HER (A/cm2)", fitColumnName, fp.IncludeHer ? FormatScientific(fp.I0Her) : "excluded");
            SetPolarizationFitCell("β_HER", fitColumnName, fp.IncludeHer ? FormatFixed(fp.BetaHer, "F4") : "excluded");

            foreach (string row in PolarizationFittedOnlyRows)
                SetPolarizationFitCell(row, staticColumnName, "n/a");

            SetPolarizationFitCell("E_corr (mV)", fitColumnName, FormatFixed(result.EcorrV * 1000.0, "F2"));
            SetPolarizationFitCell("i_corr (µA/cm2)", fitColumnName, FormatFixed(result.IcorrAcm2 * 1.0e6, "F2"));
            SetPolarizationFitCell("i@-850 mV (µA/cm2)", fitColumnName, FormatFixed(iAt850Acm2 * 1.0e6, "F2"));
            SetPolarizationFitCell("i@-1050 mV (µA/cm2)", fitColumnName, FormatFixed(iAt1050Acm2 * 1.0e6, "F2"));
            SetPolarizationFitCell("Weighted RMSE (A/cm2)", fitColumnName, FormatScientific(result.WeightedRmse));
        }

        /// <summary>
        /// Parses <paramref name="text"/> as a positive <see cref="double"/> using invariant culture.
        /// Returns <c>null</c> when the text is blank, unparseable, or non-positive.
        /// </summary>
        private static double? TryParsePositiveDouble(string text)
        {
            if (double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double val) && val > 0)
                return val;
            return null;
        }

        private static OxyColor GetSeriesColor(int index)
        {
            OxyColor[] colors =
            {
                OxyColors.DodgerBlue, OxyColors.Crimson, OxyColors.DarkOrange,
                OxyColors.ForestGreen, OxyColors.Purple, OxyColors.Brown
            };
            return colors[index % colors.Length];
        }

        // ─────────────────────────────────────────────────────────────────────────
        // Tab 2: Combine CSVs -> XML  event handlers
        // ─────────────────────────────────────────────────────────────────────────

        private void BrowseAnodicButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Select Anodic CSV file"
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                AnodicCsvPath.Text = dlg.FileName;
                AutoPopulateXmlFilename(dlg.FileName);
                UpdateXmlGenerationAvailability();
            }
        }

        private void BrowseCathodicButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Select Cathodic CSV file"
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                CathodicCsvPath.Text = dlg.FileName;
                UpdateXmlGenerationAvailability();
            }
        }

        private void BrowseXmlOutputFolderButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new FolderBrowserDialog
            {
                RootFolder = Environment.SpecialFolder.MyComputer,
                UseDescriptionForTitle = true,
                Description = "Select the folder to save the XML file in"
            };
            if (dlg.ShowDialog() == System.Windows.Forms.DialogResult.OK)
            {
                XmlOutputFolder.Text = dlg.SelectedPath;
                // Update the directory component of the suggested filename, preserving any user-edited filename.
                // Only update if XmlOutputFilename does not already contain a custom directory path.
                string existingText = XmlOutputFilename.Text?.Trim() ?? string.Empty;
                string existingFilename = System.IO.Path.GetFileName(existingText);
                if (!string.IsNullOrWhiteSpace(existingFilename))
                    XmlOutputFilename.Text = System.IO.Path.Combine(dlg.SelectedPath, existingFilename);
            }
        }

        /// <summary>
        /// Derives the suggested XML output filename from the primary CSV path.
        /// Strips trailing suffix patterns (case-insensitive):
        ///   Anodic{digits}  — e.g. "HY80Anodic1"  -> "HY80"
        ///   Cycpol{digits}  — e.g. "SteelCycpol2" -> "Steel"
        ///   Pol{digits}     — e.g. "SamplePol"    -> "Sample"
        /// Then appends "_polarization.xml".
        /// </summary>
        private void AutoPopulateXmlFilename(string primaryPath)
        {
            string baseName = System.IO.Path.GetFileNameWithoutExtension(primaryPath);
            string stripped = Regex.Replace(baseName, @"(?i)(Cycpol|Anodic|Pol)\d*$", "");
            string suggestedName = stripped + "_polarization.xml";

            string outputFolder = XmlOutputFolder.Text;
            if (!string.IsNullOrWhiteSpace(outputFolder) && Directory.Exists(outputFolder))
                XmlOutputFilename.Text = System.IO.Path.Combine(outputFolder, suggestedName);
            else
                XmlOutputFilename.Text = suggestedName;
        }

        private void GenerateXmlButton_Click(object sender, RoutedEventArgs e)
        {
            UpdateXmlGenerationAvailability();
            if (!GenerateXmlButton.IsEnabled)
            {
                System.Windows.MessageBox.Show("XML generation is only supported for CYCPOL or POTENTIODYNAMIC-derived CSV files.",
                    "Unsupported File Type", MessageBoxButton.OK, MessageBoxImage.Information);
                return;
            }

            try
            {
                string anodicPath = AnodicCsvPath.Text?.Trim();
                string cathodicPath = CathodicCsvPath.Text?.Trim();
                string outputFolder = XmlOutputFolder.Text?.Trim();
                string outputFile = XmlOutputFilename.Text?.Trim();

                // Validation
                if (string.IsNullOrEmpty(anodicPath) || !File.Exists(anodicPath))
                {
                    System.Windows.MessageBox.Show("Polarization CSV file not found. Please select a valid file.",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                // cathodicPath is optional; if provided it must exist
                if (!string.IsNullOrWhiteSpace(cathodicPath) && !File.Exists(cathodicPath))
                {
                    System.Windows.MessageBox.Show("Cathodic CSV file not found. Please select a valid file or leave the field empty to use single-file mode.",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }
                if (string.IsNullOrEmpty(outputFile))
                {
                    System.Windows.MessageBox.Show("Please specify an output filename.",
                        "Validation Error", MessageBoxButton.OK, MessageBoxImage.Warning);
                    return;
                }

                // Ensure output directory exists
                string resolvedOutputDir = System.IO.Path.GetDirectoryName(outputFile);
                if (!string.IsNullOrEmpty(resolvedOutputDir) && !Directory.Exists(resolvedOutputDir))
                    Directory.CreateDirectory(resolvedOutputDir);

                // Build metadata from UI fields
                var meta = new PolarizationCurveMetadata
                {
                    InstituteName = MetaInstituteName.Text,
                    City          = MetaCity.Text,
                    State         = MetaState.Text,
                    Country       = MetaCountry.Text,
                    UNSCode       = MetaUNSCode.Text,
                    CommonName    = MetaCommonName.Text,
                    SurfacePrep   = MetaSurfacePrep.Text,
                    ExpArea       = ParseDouble(MetaExpArea.Text, 1.0e-4),
                    ClConc        = ParseDouble(MetaClConc.Text, 1.0),
                    pH            = ParseDouble(MetapH.Text, 8.0),
                    O2conc        = ParseDouble(MetaO2Conc.Text, 2.5e-4),
                    S2conc        = ParseDouble(MetaS2Conc.Text, 0.08),
                    Temperature   = ParseDouble(MetaTemperature.Text, 50.0),
                    Conductivity  = ParseDouble(MetaConductivity.Text, 5.0),
                    Flow          = ParseDouble(MetaFlow.Text, 0.0)
                };

                PolarizationCurveXmlExporter.Export(
                    anodicPath,
                    string.IsNullOrWhiteSpace(cathodicPath) ? null : cathodicPath,
                    outputFile,
                    meta);

                XmlStatusBox.Text = $"XML export complete: {outputFile}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Error generating XML:\n{ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void BrowseAnodicPolarizationButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Select anodic (or full) polarization CSV file"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            _anodicPolarizationFilePath = dlg.FileName;
            AnodicPolarizationFilePath.Text = dlg.FileName;
        }

        private void BrowseCathodicPolarizationButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Title = "Select cathodic polarization CSV file (optional)"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            _cathodicPolarizationFilePath = dlg.FileName;
            CathodicPolarizationFilePath.Text = dlg.FileName;
        }

        private void RunPolarizationAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            if (string.IsNullOrWhiteSpace(_anodicPolarizationFilePath))
            {
                PolarizationAnalysisStatusBox.Text = "Select an anodic (or full) polarization CSV file first.";
                return;
            }

            if (!double.TryParse(PolarizationAreaText.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double areaCm2) || areaCm2 <= 0)
            {
                PolarizationAnalysisStatusBox.Text = "Invalid exposed area value.";
                return;
            }

            if (!double.TryParse(TC.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double electrolyteTemperatureC) ||
                !double.IsFinite(electrolyteTemperatureC))
            {
                PolarizationAnalysisStatusBox.Text = "Invalid electrolyte temperature value.";
                return;
            }

            if (!double.TryParse(pH.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double electrolytePh) ||
                !double.IsFinite(electrolytePh))
            {
                PolarizationAnalysisStatusBox.Text = "Invalid electrolyte pH value.";
                return;
            }

            if (!double.TryParse(ClConc.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double chlorideConcentrationM) ||
                !double.IsFinite(chlorideConcentrationM) || chlorideConcentrationM < 0.0)
            {
                PolarizationAnalysisStatusBox.Text = "Invalid chloride concentration value.";
                return;
            }

            if (!double.TryParse(MetalIonConcTextBox.Text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double metalIonConcentrationM) ||
                !double.IsFinite(metalIonConcentrationM) || metalIonConcentrationM <= 0.0)
            {
                PolarizationAnalysisStatusBox.Text = "Invalid metal ion concentration value.";
                return;
            }

            // ── Build BV user overrides from UI controls ───────────────────────────────────
            MetalSpecies selectedMetalSpecies = GetSelectedMetalSpecies();

            var bvOverrides = new BvUserOverrides
            {
                I0Her    = TryParsePositiveDouble(HerI0TextBox.Text),
                BetaHer  = TryParsePositiveDouble(HerBetaTextBox.Text),
                FixHer   = HerFixCheckBox.IsChecked == true,
                IncludeHer = HerIncludeCheckBox.IsChecked != false,

                I0Orr    = TryParsePositiveDouble(OrrI0TextBox.Text),
                BetaOrr  = TryParsePositiveDouble(OrrBetaTextBox.Text),
                IlimOrr  = TryParsePositiveDouble(OrrIlimTextBox.Text),
                FixOrr   = OrrFixCheckBox.IsChecked == true,
                IncludeOrr = OrrIncludeCheckBox.IsChecked != false,

                I0Metal   = TryParsePositiveDouble(MetalI0TextBox.Text),
                BetaMetal = TryParsePositiveDouble(MetalBetaTextBox.Text),
                FixMetal  = MetalFixCheckBox.IsChecked == true,
                IncludeMetal = MeOxIncludeCheckBox.IsChecked != false,
            };

            PolarizationAnalysisStatusBox.Text = "Running polarization analysis...";
            var result = _polarizationAnalysisService.Analyse(new PolarizationAnalysisInput
            {
                PrimaryFilePath          = _anodicPolarizationFilePath,
                CathodicFilePath         = string.IsNullOrWhiteSpace(_cathodicPolarizationFilePath) ? string.Empty : _cathodicPolarizationFilePath,
                ExposedAreaCm2           = areaCm2,
                TemperatureCelsius       = electrolyteTemperatureC,
                ElectrolytePh            = electrolytePh,
                ChlorideConcentrationM   = chlorideConcentrationM,
                MetalIonConcentrationM   = metalIonConcentrationM,
                MetalSpecies             = selectedMetalSpecies,
                ProtectionPotentialsMv   = new[] { -850.0, -1050.0 },
                UserOverrides            = bvOverrides,
            });

            if (!result.Success)
            {
                PolarizationAnalysisStatusBox.Text = result.Message;
                return;
            }

            double iAt850  = result.ProtectionCurrentDensitiesAcm2.TryGetValue("-850",  out double v850)  ? v850  : double.NaN;
            double iAt1050 = result.ProtectionCurrentDensitiesAcm2.TryGetValue("-1050", out double v1050) ? v1050 : double.NaN;
            AppendPolarizationFitResults(
                result,
                iAt850,
                iAt1050,
                electrolyteTemperatureC,
                electrolytePh,
                chlorideConcentrationM,
                metalIonConcentrationM);

            // Rebind so DataGrid picks up the newly added columns.
            PolarizationResultsGrid.ItemsSource = null;
            PolarizationResultsGrid.ItemsSource = polarizationFitResultsTable.DefaultView;

            polarizationPlotModel.Series.Clear();

            // ── Determine the potential array to use for model curves ────────────────────────────────────
            IReadOnlyList<double> modelPotentials =
                result.PlotIrCorrectedPotentialsV.Count > 0 ? result.PlotIrCorrectedPotentialsV :
                result.PlotFitPotentialsV.Count         > 0 ? result.PlotFitPotentialsV :
                result.PlotPotentialsV;

            // ── 1) Anodic file raw data — light gray dotted ───────────────────────────────────────────
            bool twoFileMode = result.PlotAnodicFilePotentialsV.Count > 0;
            if (twoFileMode)
            {
                var anodicFileSeries = new LineSeries
                {
                    Title           = "Anodic file",
                    Color           = OxyColor.FromAColor(160, OxyColors.Gray),
                    LineStyle       = LineStyle.Dot,
                    StrokeThickness = 1.0
                };
                int n = Math.Min(result.PlotAnodicFilePotentialsV.Count, result.PlotAnodicFileCurrentDensityAcm2.Count);
                for (int j = 0; j < n; j++)
                {
                    double x = Math.Max(Math.Abs(result.PlotAnodicFileCurrentDensityAcm2[j]), 1.0e-12);
                    anodicFileSeries.Points.Add(new DataPoint(x, result.PlotAnodicFilePotentialsV[j]));
                }
                if (anodicFileSeries.Points.Count > 0)
                    polarizationPlotModel.Series.Add(anodicFileSeries);

                var cathodicFileSeries = new LineSeries
                {
                    Title           = "Cathodic file",
                    Color           = OxyColor.FromAColor(160, OxyColors.Gray),
                    LineStyle       = LineStyle.Dot,
                    StrokeThickness = 1.0
                };
                int cathodicPointCount = Math.Min(result.PlotCathodicFilePotentialsV.Count, result.PlotCathodicFileCurrentDensityAcm2.Count);
                for (int j = 0; j < cathodicPointCount; j++)
                {
                    double x = Math.Max(Math.Abs(result.PlotCathodicFileCurrentDensityAcm2[j]), 1.0e-12);
                    cathodicFileSeries.Points.Add(new DataPoint(x, result.PlotCathodicFilePotentialsV[j]));
                }
                if (cathodicFileSeries.Points.Count > 0)
                    polarizationPlotModel.Series.Add(cathodicFileSeries);
            }

            // ── 2) Combined anodic + cathodic polarization curve — light gray dashed ─────────────────
            var mergedSeries = new LineSeries
            {
                Title           = "Combined data",
                Color           = OxyColor.FromAColor(160, OxyColors.Gray),
                LineStyle       = LineStyle.Dash,
                StrokeThickness = 1.5
            };
            int dataCount = Math.Min(result.PlotPotentialsV.Count, result.PlotCurrentDensityAcm2.Count);
            for (int j = 0; j < dataCount; j++)
            {
                double x = Math.Max(Math.Abs(result.PlotCurrentDensityAcm2[j]), 1.0e-12);
                mergedSeries.Points.Add(new DataPoint(x, result.PlotPotentialsV[j]));
            }
            if (mergedSeries.Points.Count > 0)
                polarizationPlotModel.Series.Add(mergedSeries);

            // ── 3) Metal oxidation BV component — light blue ──────────────────────────────────────────
            if (result.PlotIMetalBvAcm2.Count > 0)
            {
                var metalSeries = new LineSeries
                {
                    Title           = $"{selectedMetalSpecies} oxidation BV",
                    Color           = OxyColors.LightBlue,
                    LineStyle       = LineStyle.Solid,
                    StrokeThickness = 1.5
                };
                int n = Math.Min(modelPotentials.Count, result.PlotIMetalBvAcm2.Count);
                for (int j = 0; j < n; j++)
                {
                    double x = Math.Max(Math.Abs(result.PlotIMetalBvAcm2[j]), 1.0e-12);
                    metalSeries.Points.Add(new DataPoint(x, modelPotentials[j]));
                }
                if (metalSeries.Points.Count > 0)
                    polarizationPlotModel.Series.Add(metalSeries);
            }

            // ── 4) ORR BV component — green ───────────────────────────────────────────────────────────
            if (result.PlotIorrAcm2.Count > 0)
            {
                var orrSeries = new LineSeries
                {
                    Title           = "ORR BV",
                    Color           = OxyColors.ForestGreen,
                    LineStyle       = LineStyle.Solid,
                    StrokeThickness = 1.5
                };
                int n = Math.Min(modelPotentials.Count, result.PlotIorrAcm2.Count);
                for (int j = 0; j < n; j++)
                {
                    double x = Math.Max(Math.Abs(result.PlotIorrAcm2[j]), 1.0e-12);
                    orrSeries.Points.Add(new DataPoint(x, modelPotentials[j]));
                }
                if (orrSeries.Points.Count > 0)
                    polarizationPlotModel.Series.Add(orrSeries);
            }

            // ── 5) HER BV component — orange ─────────────────────────────────────────────────────────
            if (result.PlotIherAcm2.Count > 0)
            {
                var herSeries = new LineSeries
                {
                    Title           = "HER BV",
                    Color           = OxyColors.DarkOrange,
                    LineStyle       = LineStyle.Solid,
                    StrokeThickness = 1.5
                };
                int n = Math.Min(modelPotentials.Count, result.PlotIherAcm2.Count);
                for (int j = 0; j < n; j++)
                {
                    double x = Math.Max(Math.Abs(result.PlotIherAcm2[j]), 1.0e-12);
                    herSeries.Points.Add(new DataPoint(x, modelPotentials[j]));
                }
                if (herSeries.Points.Count > 0)
                    polarizationPlotModel.Series.Add(herSeries);
            }

            // ── 6) Total BV model curve (HER + ORR + Metal) — solid black ─────────────────────────────
            if (result.PlotModelCurrentDensityAcm2.Count > 0)
            {
                var modelSeries = new LineSeries
                {
                    Title           = "BV model (total)",
                    Color           = OxyColors.Black,
                    LineStyle       = LineStyle.Solid,
                    StrokeThickness = 2.0
                };
                int n = Math.Min(modelPotentials.Count, result.PlotModelCurrentDensityAcm2.Count);
                for (int j = 0; j < n; j++)
                {
                    double x = Math.Max(Math.Abs(result.PlotModelCurrentDensityAcm2[j]), 1.0e-12);
                    modelSeries.Points.Add(new DataPoint(x, modelPotentials[j]));
                }
                if (modelSeries.Points.Count > 0)
                    polarizationPlotModel.Series.Add(modelSeries);
            }

            polarizationPlotModel.InvalidatePlot(true);

            PolarizationAnalysisStatusBox.Text = "Polarization analysis completed.";
        }

        private void ExportFitsToCsvButton_Click(object sender, RoutedEventArgs e)
        {
            if (polarizationFitResultsTable.Columns.Count < 3)
            {
                PolarizationAnalysisStatusBox.Text = "Run a polarization fit before exporting CSV values.";
                return;
            }

            using var dlg = new SaveFileDialog
            {
                Title = "Export fit columns to CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                DefaultExt = "csv",
                FileName = "polarization_fit_setup.csv"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                string staticColumnName = polarizationFitResultsTable.Columns[polarizationFitResultsTable.Columns.Count - 2].ColumnName;
                string fitColumnName = polarizationFitResultsTable.Columns[polarizationFitResultsTable.Columns.Count - 1].ColumnName;
                var lines = new List<string> { "Entry" };

                foreach (DataRow row in polarizationFitResultsTable.Rows)
                {
                    string parameterName = row["Parameter"]?.ToString() ?? string.Empty;
                    lines.Add(QuoteCsvValue($"Static|{parameterName}|{row[staticColumnName]?.ToString() ?? string.Empty}"));
                    lines.Add(QuoteCsvValue($"Fit|{parameterName}|{row[fitColumnName]?.ToString() ?? string.Empty}"));
                }

                File.WriteAllLines(dlg.FileName, lines);
                PolarizationAnalysisStatusBox.Text = $"Fit CSV exported: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to export fit CSV:\n{ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private void ImportFitSetupButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Title = "Import fit setup CSV",
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                var staticValues = new Dictionary<string, string>(StringComparer.Ordinal);
                var fitValues = new Dictionary<string, string>(StringComparer.Ordinal);

                foreach (string line in File.ReadLines(dlg.FileName))
                {
                    string value = UnquoteCsvValue(line);
                    if (string.IsNullOrWhiteSpace(value) || string.Equals(value, "Entry", StringComparison.OrdinalIgnoreCase))
                        continue;

                    string[] parts = value.Split('|', 3);
                    if (parts.Length != 3)
                        continue;

                    if (string.Equals(parts[0], "Static", StringComparison.OrdinalIgnoreCase))
                        staticValues[parts[1]] = parts[2];
                    else if (string.Equals(parts[0], "Fit", StringComparison.OrdinalIgnoreCase))
                        fitValues[parts[1]] = parts[2];
                }

                if (staticValues.Count == 0 && fitValues.Count == 0)
                    throw new InvalidDataException("The selected CSV does not contain any exported fit entries.");

                foreach (string parameterName in staticValues.Keys.Concat(fitValues.Keys).Distinct(StringComparer.Ordinal))
                    ApplyImportedBvSetup(parameterName, GetImportedValue(staticValues, parameterName), GetImportedValue(fitValues, parameterName));

                PolarizationAnalysisStatusBox.Text = $"Fit setup imported: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to import fit CSV:\n{ex.Message}",
                    "Import Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Exports the current polarization plot (including legend) to a user-selected PNG file.
        /// </summary>
        private void SavePolarizationPlotButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new SaveFileDialog
            {
                Title = "Save polarization plot",
                Filter = "PNG image (*.png)|*.png",
                DefaultExt = "png",
                FileName = "polarization_plot.png"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            try
            {
                const int exportWidth  = 1200;
                const int exportHeight = 900;
                var exporter = new OxyPlot.Wpf.PngExporter { Width = exportWidth, Height = exportHeight };
                using var stream = System.IO.File.Create(dlg.FileName);
                exporter.Export(polarizationPlotModel, stream);
                PolarizationAnalysisStatusBox.Text = $"Plot saved: {dlg.FileName}";
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to save plot image:\n{ex.Message}",
                    "Export Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        /// <summary>
        /// Estimates the ORR limiting current density from temperature, Cl⁻ concentration,
        /// and diffusion-layer thickness, then populates <see cref="OrrIlimTextBox"/>.
        /// </summary>
        private void CalcIlimButton_Click(object sender, RoutedEventArgs e)
        {
            if (!TryParseIlimInputs(out double tempC, out double clM, out double deltaMicrons, showErrors: true))
                return;

            try
            {
                double deltaCm = deltaMicrons * 1.0e-4;
                double ilim = DissolvedOxygenCalculator.CalcOrrIlimAcm2(tempC, clM, deltaCm);
                OrrIlimTextBox.Text = ilim.ToString("E3", CultureInfo.InvariantCulture);
            }
            catch (Exception ex)
            {
                System.Windows.MessageBox.Show($"Failed to calculate i_lim:\n{ex.Message}",
                    "Calculation Error", MessageBoxButton.OK, MessageBoxImage.Error);
            }
        }

        private static double ParseDouble(string text, double defaultValue)
        {
            if (double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;
            return defaultValue;
        }
    }
}
