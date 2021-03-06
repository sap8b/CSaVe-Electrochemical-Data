<img src = "https://github.com/sap8b/CSaVe-Electrochemical-Data/blob/master/CSaVe Electrochemical Data Logo.png" align = "left" width = "1024" height = "200">

# 
A utility for extracting data from electrochemical experimentation datafiles and saving them as CSV files for easier plotting.

## Introduction
* CSaVe-Electrochemical-Data is an x86 app utility that provides batch processing to extract impedance data and column names from DTA files and convert them to comma-separated-values (CSV) files.  
* The CSV files have the same name as the DTA files but can be stored in a new, specified folder for easier visualization and analysis.

## Background
This project started as a Windows Presentation Foundation (WPF) project for use as locally to speed up visualization and analysis of EIS experimental datafiles.  However, by adding a paakaging project to the original repository, the project was able to be converted to a Win32 app that could be distributed through the Microsoft Store.

## Code
This project was written in C# and XAML using Microsoft Visual Studio 2019. 

## Installation
The app is available here: https://www.microsoft.com/store/productId/9MWJ4GK6S7ZK

## Next Steps
* Add the capability to extract data for the following filetypes:
    - <strike>Corrosion potential</strike>
    - <strike>Cyclic polarization</strike>
    - <strike>Potentiodynamic polarization</strike>
    - <strike>Cyclic voltammetry</strike>
    - <strike>Potentiostatic EIS</strike>
    - <strike>Galvanostatic EIS</strike>
    - <strike>BiPotentiostat RDE</strike>
    - <strike>Galvanostatic</strike>
    - <strike>Potentiostatic</strike>
* Add support for other manufacturers datafiles - if they aren't already easily plottable
* Add plotting scripts for some common plotting packages
