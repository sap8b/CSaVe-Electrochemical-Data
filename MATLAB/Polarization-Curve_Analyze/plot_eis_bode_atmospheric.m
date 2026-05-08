function plot_eis_bode_atmospheric
    %CombineMLEISData - Loads individual ML datasets for EIS fits and
    %combines them into a larger dataset
    %
    %The purpose of this function is to load individual ML datasets for EIS
    % fits and combine them into a larger dataset. The file must be a comma 
    %separated variable type file.
    %
    % Syntax:  CombineMLEISData(obj, baseDirectory)
    %
    % Inputs:
    %    baseDirectory - Directory to find the files in neededFileNames
    %
    % Outputs:
    %    Sets class currentData and potentialData
    %
    % Other m-files required: none
    % Subfunctions: none
    % MAT-files required: none
    %
    % See also: 
    %
    %==========================================================================
    % Author:   Steve Policastro, Ph.D., Materials Science
    % Center for Corrosion Science and Engineering, U.S. Naval Research
    % Laboratory
    % email address: steven.policastro@nrl.navy.mil  
    % Website: 
    % January 2022; Last revision: 07 January 2022
    %==========================================================================
    %------------- BEGIN CODE --------------
    clc;
    clear all;

    %========================================================================== 
    tick_label_size = 16;
    axis_label_size = 18;
    title_label_size = 20;
    axis_line_width = 3;
    font_weight = 'bold';
    plot_line_width = 3;
    plot_line_width_2 = 2;
    marker_size = 8;
    colorVec = {[0 0 1],[0 0 0.95],[0 0 0.9],[0 0 0.85], ...
        [0 0 0.8],[0 0 0.75],[0 0 0.7],[0 0 0.65], ...
        [0 0 0.6],[0 0 0.55],[0 0 0.5],[0 0 0.45],[0 0 0.4], ...
        [0 0 0.35]};
    colorVec1 = {[1.0 0 0],[0.95 0 0],[0.9 0 0],[0.85 0 0], ...
        [0.8 0 0],[0.75 0 0],[0.7 0 0],[0.65 0 0], ...
        [0.6 0 0],[0.55 0 0],[0.5 0 0],[0.45 0 0],[0.4 0 0], ...
        [0.35, 0, 0]};    
    markerVec = {'o','s','d','o','s','d','o','s','d','o','s','d','o','s'};
    markerVec1 = {'^','<','>','^','<','>','^','<','>','^','<','>','^','<'};
    linVec = {'none','none','none','none','none','none','none','none', ...
        'none','none','none','none','none','none'}; 
    %========================================================================== 
    extension = '.csv';
    headerLinesIn = 0;
    delimiterIn = ',';

    baseDirectory = fullfile('C:','Users','steve','source','repos','EISData_Fitting','bin','Debug','net6.0\','EIS Data - 138');
%     baseDirectory = fullfile('C:','Users','Steve Policastro','OneDrive','SERDP','Matlab Files');
    fn = 'A18_04_Coatings_fit_out';
    fullFileName = fullfile(baseDirectory, strcat(fn,extension));
    MLData = readtable(fullFileName);
    MLData.Properties.VariableNames = {'xD','yD','xF','yF'};
    x = MLData.xD;
    y1 = MLData.yD;
    y2 = MLData.yD;

    figure(1)
    colormap parula
    x0=10;
    y0=10;
    width=800;
    height=600;
    
    set(gcf,'units','points','position',[x0,y0,width,height])        
    
    hold on
    plot(x,y1,'k+','LineWidth', plot_line_width,'MarkerSize',marker_size)   %,'LineWidth', plot_line_width
    plot(x,y2,'-r','LineWidth', plot_line_width,'MarkerSize',marker_size)   %,'LineWidth', plot_line_width

%         title(string(plot_title{1,i}), 'FontSize', title_label_size)
    xlabel('Frequency (Hz)', 'FontSize', axis_label_size,'FontWeight',font_weight)
    ylabel('Z_{mod} (\Omega)', 'FontSize', axis_label_size,'FontWeight',font_weight) 

    ax = gca;
    ax.XScale = 'log';
   
%     ylim([1.0e3 1.0e9]);
    ax.YScale = 'log';            
    
    xlim([1.0e-1 1.0e5]);
    ax.FontSize = tick_label_size;
    ax.FontWeight = font_weight;
    ax.LineWidth = axis_line_width;
    ax.XMinorTick = 'on';
    ax.YMinorTick = 'on';           

    box on
%         legend(legend_string(1,:),'Location','northeast')
%         legend boxoff
    axis square       
    hold off        
%         legend_string = s(i).time;
    
end