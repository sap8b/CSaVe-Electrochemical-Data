using System;
using System.Collections.Generic;
using System.ComponentModel;
using System.IO;
using System.Linq;
using System.Text;
using System.Threading.Tasks;
using System.Windows;
using System.Windows.Controls;
using System.Windows.Data;
using System.Windows.Documents;
using System.Windows.Forms;
using System.Windows.Input;
using System.Windows.Media;
using System.Windows.Media.Imaging;
using System.Windows.Navigation;
using System.Windows.Shapes;

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

        public MainWindow()
        {
            InitializeComponent();
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
    }
}
