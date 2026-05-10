using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Forms;
using CSaVe_Electrochemical_Data.Models;
using CSaVe_Electrochemical_Data.Services;
using OxyPlot;
using OxyPlot.Axes;
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

        private readonly PythonAnalysisService pythonAnalysisService;
        private readonly IPolarizationAnalysisService _polarizationAnalysisService;
        private string _anodicPolarizationFilePath = string.Empty;
        private string _cathodicPolarizationFilePath = string.Empty;
        private readonly List<string> eisAnalysisFiles = new();
        private readonly PlotModel polarizationPlotModel = new();
        private readonly PlotModel eisNyquistPlotModel = new();
        private readonly PlotModel eisBodePlotModel = new();
        private static readonly Regex PdTokenRegex = new(@"(^|[^a-z])pd([^a-z]|$)", RegexOptions.Compiled);

        public MainWindow()
        {
            InitializeComponent();

            pythonAnalysisService = new PythonAnalysisService(FindRepositoryRoot());

            _polarizationAnalysisService = new PolarizationAnalysisService(
                new PolarizationCsvReader(),
                new MonotonicityFilter(),
                new PolarizationCurveJoiner(),
                new BvCurveFitter());

            AnodicCsvPath.TextChanged += (_, _) => UpdateXmlGenerationAvailability();
            CathodicCsvPath.TextChanged += (_, _) => UpdateXmlGenerationAvailability();
            UpdateXmlGenerationAvailability();

            InitializePlotModels();
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

        private sealed class PolarizationResultRow
        {
            public string File { get; set; } = string.Empty;
            public double Ecorr_mV { get; set; }
            public double Icorr_uAcm2 { get; set; }
            public double I_ox_uAcm2 { get; set; }
            public double Ilim_uAcm2 { get; set; }
            public double HER_Eeq_mV { get; set; }
            public double I_at_neg850mV_uAcm2 { get; set; }
            public double I_at_neg1050mV_uAcm2 { get; set; }
        }

        private sealed class EisResultRow
        {
            public string File { get; set; } = string.Empty;
            public string Model { get; set; } = string.Empty;
            public string Parameters { get; set; } = string.Empty;
        }

        private static string FindRepositoryRoot()
        {
            var dir = new DirectoryInfo(AppContext.BaseDirectory);
            while (dir is not null)
            {
                if (File.Exists(Path.Combine(dir.FullName, "CSaVe Electrochemical Data.csproj")))
                    return dir.FullName;
                dir = dir.Parent;
            }

            throw new DirectoryNotFoundException("Unable to locate repository root for Python analysis scripts.");
        }

        private void InitializePlotModels()
        {
            polarizationPlotModel.Title = "Polarization (|i| vs E)";
            polarizationPlotModel.Axes.Add(new LogarithmicAxis { Position = AxisPosition.Bottom, Title = "Current Density (A/cm²)", Minimum = 1.0e-12 });
            polarizationPlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "Potential (V)" });
            PolarizationPlotView.Model = polarizationPlotModel;

            eisNyquistPlotModel.Title = "Nyquist";
            eisNyquistPlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Bottom, Title = "Z' (Ohm)" });
            eisNyquistPlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Left, Title = "-Z'' (Ohm)" });
            EisNyquistPlotView.Model = eisNyquistPlotModel;

            eisBodePlotModel.Title = "Bode";
            eisBodePlotModel.Axes.Add(new LogarithmicAxis { Position = AxisPosition.Bottom, Title = "Frequency (Hz)", Minimum = 1.0e-3 });
            eisBodePlotModel.Axes.Add(new LogarithmicAxis { Position = AxisPosition.Left, Title = "|Z| (Ohm)", Key = "MagAxis", Minimum = 1.0e-3 });
            eisBodePlotModel.Axes.Add(new LinearAxis { Position = AxisPosition.Right, Title = "Phase (deg)", Key = "PhaseAxis" });
            EisBodePlotView.Model = eisBodePlotModel;
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

            string[] cols = header.Split(',').Select(c => c.Trim().ToLowerInvariant()).ToArray();
            bool hasVf = cols.Contains("vf");
            bool hasIm = cols.Contains("im");
            bool looksLikeEis = cols.Contains("freq") || cols.Contains("zreal") || cols.Contains("zimag");

            if (looksLikeEis)
                return XmlEligibleCsvType.UNSUPPORTED;
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

        private static string FormatMeanStd(IEnumerable<double> vals)
        {
            var arr = vals.Where(v => !double.IsNaN(v) && !double.IsInfinity(v)).ToArray();
            if (arr.Length == 0)
                return "n/a";

            double mean = arr.Average();
            double std = arr.Length > 1
                ? Math.Sqrt(arr.Sum(v => (v - mean) * (v - mean)) / (arr.Length - 1))
                : 0.0;
            return $"{mean:F2} ± {std:F2}";
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
        // Tab 2: Combine CSVs → XML  event handlers
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
        ///   Anodic{digits}  — e.g. "HY80Anodic1"  → "HY80"
        ///   Cycpol{digits}  — e.g. "SteelCycpol2" → "Steel"
        ///   Pol{digits}     — e.g. "SamplePol"    → "Sample"
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

            PolarizationAnalysisStatusBox.Text = "Running polarization analysis...";
            var result = _polarizationAnalysisService.Analyse(new PolarizationAnalysisInput
            {
                PrimaryFilePath          = _anodicPolarizationFilePath,
                CathodicFilePath         = string.IsNullOrWhiteSpace(_cathodicPolarizationFilePath) ? string.Empty : _cathodicPolarizationFilePath,
                ExposedAreaCm2           = areaCm2,
                ProtectionPotentialsMv   = new[] { -850.0, -1050.0 }
            });

            if (!result.Success)
            {
                PolarizationAnalysisStatusBox.Text = result.Message;
                return;
            }

            double iAt850  = result.ProtectionCurrentDensitiesAcm2.TryGetValue("-850",  out double v850)  ? v850  : double.NaN;
            double iAt1050 = result.ProtectionCurrentDensitiesAcm2.TryGetValue("-1050", out double v1050) ? v1050 : double.NaN;

            var rows = new List<PolarizationResultRow>
            {
                new PolarizationResultRow
                {
                    File                 = Path.GetFileName(_anodicPolarizationFilePath),
                    Ecorr_mV             = result.EcorrV * 1000.0,
                    Icorr_uAcm2          = result.IcorrAcm2 * 1.0e6,
                    I_ox_uAcm2           = double.IsNaN(result.IOxAcm2) ? double.NaN : result.IOxAcm2 * 1.0e6,
                    Ilim_uAcm2           = result.IlimOrrAcm2 * 1.0e6,
                    HER_Eeq_mV           = result.HerEquilibriumV * 1000.0,
                    I_at_neg850mV_uAcm2  = iAt850  * 1.0e6,
                    I_at_neg1050mV_uAcm2 = iAt1050 * 1.0e6
                }
            };
            PolarizationResultsGrid.ItemsSource = rows;

            string summary = string.Join(Environment.NewLine, new[]
            {
                $"E_corr (mV): {FormatMeanStd(rows.Select(r => r.Ecorr_mV))}",
                $"i_corr (uA/cm²): {FormatMeanStd(rows.Select(r => r.Icorr_uAcm2))}",
                $"i_ox (uA/cm²): {FormatMeanStd(rows.Select(r => r.I_ox_uAcm2))}",
                $"i_lim ORR (uA/cm²): {FormatMeanStd(rows.Select(r => r.Ilim_uAcm2))}",
                $"HER E_eq (mV): {FormatMeanStd(rows.Select(r => r.HER_Eeq_mV))}",
                $"i@-850 mV (uA/cm²): {FormatMeanStd(rows.Select(r => r.I_at_neg850mV_uAcm2))}",
                $"i@-1050 mV (uA/cm²): {FormatMeanStd(rows.Select(r => r.I_at_neg1050mV_uAcm2))}"
            });
            PolarizationSummaryBox.Text = summary;

            polarizationPlotModel.Series.Clear();

            // ── Determine the potential array to use for model curves ────────────────────────────────────
            IReadOnlyList<double> modelPotentials =
                result.PlotIrCorrectedPotentialsV.Count > 0 ? result.PlotIrCorrectedPotentialsV :
                result.PlotFitPotentialsV.Count         > 0 ? result.PlotFitPotentialsV :
                result.PlotPotentialsV;

            // ── Split raw data into anodic (i > 0) and cathodic (i < 0) branches for separate styling ────
            var anodicSeries = new LineSeries
            {
                Title           = "Anodic branch",
                Color           = OxyColors.Gray,
                LineStyle       = LineStyle.Dash,
                StrokeThickness = 0.8
            };

            var cathodicSeries = new LineSeries
            {
                Title           = "Cathodic branch",
                Color           = OxyColors.Gray,
                LineStyle       = LineStyle.Dot,
                StrokeThickness = 0.8
            };

            int dataCount = Math.Min(result.PlotPotentialsV.Count, result.PlotCurrentDensityAcm2.Count);
            for (int j = 0; j < dataCount; j++)
            {
                double x = Math.Max(Math.Abs(result.PlotCurrentDensityAcm2[j]), 1.0e-12);
                double y = result.PlotPotentialsV[j];

                if (result.PlotCurrentDensityAcm2[j] >= 0)
                    anodicSeries.Points.Add(new DataPoint(x, y));
                else
                    cathodicSeries.Points.Add(new DataPoint(x, y));
            }
            polarizationPlotModel.Series.Add(anodicSeries);
            polarizationPlotModel.Series.Add(cathodicSeries);

            // ── Merged / combined data trace: gray solid line (all |i| vs E) ─────────────────────────────
            var mergedSeries = new LineSeries
            {
                Title           = "Merged data",
                Color           = OxyColors.Gray,
                LineStyle       = LineStyle.Solid,
                StrokeThickness = 1.5
            };
            for (int j = 0; j < dataCount; j++)
            {
                double x = Math.Max(Math.Abs(result.PlotCurrentDensityAcm2[j]), 1.0e-12);
                mergedSeries.Points.Add(new DataPoint(x, result.PlotPotentialsV[j]));
            }
            polarizationPlotModel.Series.Add(mergedSeries);

            // ── Modeled / fitted curve: thick black dashed line ───────────────────────────────────────────
            var fitSeries = new LineSeries
            {
                Title           = "BV model",
                Color           = OxyColors.Black,
                LineStyle       = LineStyle.Dash,
                StrokeThickness = 2.5
            };
            int fitCount = Math.Min(modelPotentials.Count, result.PlotModelCurrentDensityAcm2.Count);
            for (int j = 0; j < fitCount; j++)
            {
                double x = Math.Max(Math.Abs(result.PlotModelCurrentDensityAcm2[j]), 1.0e-12);
                fitSeries.Points.Add(new DataPoint(x, modelPotentials[j]));
            }
            polarizationPlotModel.Series.Add(fitSeries);

            // ── Component curves (i_ox, i_ORR, i_HER) — keep existing styling, omit if empty ─────────────
            if (result.PlotIoxAcm2.Count > 0)
            {
                var ioxSeries = new LineSeries { Title = "i_ox (anodic)", Color = OxyColors.DodgerBlue, LineStyle = LineStyle.Solid, StrokeThickness = 1.2 };
                int nc = Math.Min(modelPotentials.Count, result.PlotIoxAcm2.Count);
                for (int j = 0; j < nc; j++)
                {
                    double x = Math.Max(Math.Abs(result.PlotIoxAcm2[j]), 1.0e-12);
                    ioxSeries.Points.Add(new DataPoint(x, modelPotentials[j]));
                }
                polarizationPlotModel.Series.Add(ioxSeries);
            }

            if (result.PlotIorrAcm2.Count > 0)
            {
                var orrSeries = new LineSeries { Title = "i_ORR", Color = OxyColors.ForestGreen, LineStyle = LineStyle.Solid, StrokeThickness = 1.2 };
                int nc = Math.Min(modelPotentials.Count, result.PlotIorrAcm2.Count);
                for (int j = 0; j < nc; j++)
                {
                    double x = Math.Max(Math.Abs(result.PlotIorrAcm2[j]), 1.0e-12);
                    orrSeries.Points.Add(new DataPoint(x, modelPotentials[j]));
                }
                polarizationPlotModel.Series.Add(orrSeries);
            }

            if (result.PlotIherAcm2.Count > 0)
            {
                var herSeries = new LineSeries { Title = "i_HER", Color = OxyColors.DarkOrange, LineStyle = LineStyle.Solid, StrokeThickness = 1.2 };
                int nc = Math.Min(modelPotentials.Count, result.PlotIherAcm2.Count);
                for (int j = 0; j < nc; j++)
                {
                    double x = Math.Max(Math.Abs(result.PlotIherAcm2[j]), 1.0e-12);
                    herSeries.Points.Add(new DataPoint(x, modelPotentials[j]));
                }
                polarizationPlotModel.Series.Add(herSeries);
            }

            polarizationPlotModel.InvalidatePlot(true);

            PolarizationAnalysisStatusBox.Text = "Polarization analysis completed.";
        }

        private void BrowseEisFilesButton_Click(object sender, RoutedEventArgs e)
        {
            using var dlg = new OpenFileDialog
            {
                Filter = "CSV files (*.csv)|*.csv|All files (*.*)|*.*",
                Multiselect = true,
                Title = "Select EIS CSV file(s)"
            };
            if (dlg.ShowDialog() != System.Windows.Forms.DialogResult.OK)
                return;

            eisAnalysisFiles.Clear();
            eisAnalysisFiles.AddRange(dlg.FileNames);
            EisFilesStatusText.Text = $"{eisAnalysisFiles.Count} EIS file(s) selected.";
        }

        private void RunEisAnalysisButton_Click(object sender, RoutedEventArgs e)
        {
            if (eisAnalysisFiles.Count == 0)
            {
                EisAnalysisStatusBox.Text = "Select one or more EIS CSV files first.";
                return;
            }

            string model = (EisModelComboBox.SelectedItem as ComboBoxItem)?.Content?.ToString() ?? "randles_cpe_w";
            EisAnalysisStatusBox.Text = $"Running EIS analysis ({model}) in system Python...";

            var response = pythonAnalysisService.RunEis(new EisAnalysisRequest
            {
                Files = eisAnalysisFiles.ToList(),
                Model = model
            });

            if (!response.Success)
            {
                EisAnalysisStatusBox.Text = response.Message;
                return;
            }

            var rows = response.Files.Select(f => new EisResultRow
            {
                File = Path.GetFileName(f.File),
                Model = f.Model,
                Parameters = string.Join(", ", f.Fit_Parameters.Select(kv => $"{kv.Key}={kv.Value:G4}"))
            }).ToList();
            EisResultsGrid.ItemsSource = rows;

            eisNyquistPlotModel.Series.Clear();
            eisBodePlotModel.Series.Clear();

            if (response.Files.Count > 0)
            {
                var first = response.Files[0];
                var nyquistData = new LineSeries { Title = "Data", Color = OxyColors.DodgerBlue };
                for (int i = 0; i < Math.Min(first.Plot.Zreal_Ohm.Count, first.Plot.Zimag_Ohm.Count); i++)
                    nyquistData.Points.Add(new DataPoint(first.Plot.Zreal_Ohm[i], -first.Plot.Zimag_Ohm[i]));
                eisNyquistPlotModel.Series.Add(nyquistData);

                var nyquistFit = new LineSeries { Title = "Fit", Color = OxyColors.Crimson, LineStyle = LineStyle.Dash };
                for (int i = 0; i < Math.Min(first.Plot.Zreal_Fit_Ohm.Count, first.Plot.Zimag_Fit_Ohm.Count); i++)
                    nyquistFit.Points.Add(new DataPoint(first.Plot.Zreal_Fit_Ohm[i], -first.Plot.Zimag_Fit_Ohm[i]));
                eisNyquistPlotModel.Series.Add(nyquistFit);

                var bodeMag = new LineSeries { Title = "|Z| Data", Color = OxyColors.ForestGreen, YAxisKey = "MagAxis" };
                var bodeMagFit = new LineSeries { Title = "|Z| Fit", Color = OxyColors.DarkOrange, LineStyle = LineStyle.Dash, YAxisKey = "MagAxis" };
                var bodePhase = new LineSeries { Title = "Phase Data", Color = OxyColors.Purple, YAxisKey = "PhaseAxis" };
                var bodePhaseFit = new LineSeries { Title = "Phase Fit", Color = OxyColors.Gray, LineStyle = LineStyle.Dash, YAxisKey = "PhaseAxis" };

                int n = first.Plot.Freq_Hz.Count;
                for (int i = 0; i < n; i++)
                {
                    double f = Math.Max(first.Plot.Freq_Hz[i], 1.0e-6);
                    if (i < first.Plot.Zmod_Ohm.Count) bodeMag.Points.Add(new DataPoint(f, Math.Max(first.Plot.Zmod_Ohm[i], 1.0e-9)));
                    if (i < first.Plot.Zmod_Fit_Ohm.Count) bodeMagFit.Points.Add(new DataPoint(f, Math.Max(first.Plot.Zmod_Fit_Ohm[i], 1.0e-9)));
                    if (i < first.Plot.Phase_Deg.Count) bodePhase.Points.Add(new DataPoint(f, first.Plot.Phase_Deg[i]));
                    if (i < first.Plot.Phase_Fit_Deg.Count) bodePhaseFit.Points.Add(new DataPoint(f, first.Plot.Phase_Fit_Deg[i]));
                }

                eisBodePlotModel.Series.Add(bodeMag);
                eisBodePlotModel.Series.Add(bodeMagFit);
                eisBodePlotModel.Series.Add(bodePhase);
                eisBodePlotModel.Series.Add(bodePhaseFit);
            }

            eisNyquistPlotModel.InvalidatePlot(true);
            eisBodePlotModel.InvalidatePlot(true);
            EisAnalysisStatusBox.Text = response.Message;
        }

        private static double ParseDouble(string text, double defaultValue)
        {
            if (double.TryParse(text?.Trim(), NumberStyles.Float, CultureInfo.InvariantCulture, out double result))
                return result;
            return defaultValue;
        }
    }
}
