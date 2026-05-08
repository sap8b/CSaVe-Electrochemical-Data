function O2Diffusivity_RevB

    clc;
    clear all;   

    %===================================
    tick_label_size = 16;
    axis_label_size = 18;
    title_label_size = 20;
    axis_line_width = 3;
    font_weight = 'bold';
    plot_line_width = 3;
    plot_line_width_2 = 2;    
    marker_size = 8;
    symbol_string = {'o','+','^','s','<','>'};
    %===================================

    % Analytica Chimica Acta, Volume 279, Issue 2, 15 July 1993, Pages 213-21
    TC_1 = [20.0, 25.0, 30.0, 35.0, 40.0]; %C
    TK = (TC_1 + 273.15);   

    cCl_1 = [0.0, 0.0846, 0.154, 0.282, 0.4231, 0.5642, 0.7052, 0.8463]; %M   

    DO2_1 = [1.98, 1.94, 1.96, 1.95, 1.93, 1.91, 1.96, 1.97; ... % 
        2.26, 2.20, 2.22, 2.21, 2.12, 2.17, 2.25, 2.24; ... %
        2.56, 2.50, 2.47, 2.48, 2.38, 2.47, 2.55, 2.51; ...
        2.96, 2.80, 2.74, 2.71, 2.69, 2.78, 2.88, 2.81; ...
        3.26, 3.15, 3.12, 3.14, 3.00, 3.12, 3.23, 3.13].*1.0e-5; %cm2/s

    diffusionModelConcParams = zeros(length(TK),6);
    diffusionModelTempParams = zeros(6,3); 
    legend_string = cell(size(TK));
    
    cPlot = 0.0:0.01:5.2;
    BMatrix = zeros(length(TK),length(cPlot));

    AMatrix =[cCl_1',DO2_1(1:5,:)'];
    fn = 'DatO2.csv';
    writematrix(AMatrix,fn);

    figure(10)
    hold on
    for i = 1:length(TK)
        legend_string{i} = strcat('T =',num2str(round(TK(i))),'^oC');
        plot(cCl_1,DO2_1(i,:),strcat('r',symbol_string{i}),'MarkerSize',marker_size,'LineWidth', plot_line_width)        
    end
    xc = 1;
    yc = 2;
    for i = 1:length(TK)
        if i == 1
            inddiesToFit = [4,5,6]; %[1,4,5,6];
            x = cCl_1(inddiesToFit);
            y = DO2_1(i,inddiesToFit);              
        elseif i == 2
            inddiesToFit = [1,2,3,5]; %[1,2,5];
            x = cCl_1(inddiesToFit);
            y = DO2_1(i,inddiesToFit);           
        elseif i == 3
            inddiesToFit = [1,2,3,5];
            x = cCl_1(inddiesToFit);
            y = DO2_1(i,inddiesToFit);            
        elseif i == 4
            inddiesToFit = [1,2,3];
            x = cCl_1(inddiesToFit);
            y = DO2_1(i,inddiesToFit);           
        else
            inddiesToFit = [1,2,3,5];
            x = cCl_1(inddiesToFit);
            y = DO2_1(i,inddiesToFit);           
        end

        beta = [1.0e-7, 0.065, 0.01, 0.06, 0.1, 500.0]; 
        DO2mdl = fitnlm(x,y,@(beta,x)StokesModel(beta, i, x),beta);
        diffusionModelConcParams(i,:) = DO2mdl.Coefficients.Estimate; 

        
        mdlD = feval(DO2mdl,cPlot);  
        plot(cPlot, mdlD, '-b','LineWidth', plot_line_width-1)
        BMatrix(xc,:) = cPlot';
        BMatrix(yc,:) = mdlD';
        xc = xc+2;
        yc = yc+2;
    end
    axis square
    box on
    xlabel('[Cl^-] (M)', 'FontSize', axis_label_size,'FontWeight',font_weight)
    ylabel('Diffusion coefficient (cm^2/s)', 'FontSize', axis_label_size,'FontWeight',font_weight)
    
    ax = gca;
    ax.FontSize = tick_label_size;
    ax.FontWeight = font_weight;
    ax.LineWidth = axis_line_width;
    ax.XMinorTick = 'on';
    ax.YMinorTick = 'on';     

    legend(legend_string, 'Location', 'best')
    legend boxoff
    hold off    

    fn2 = 'O2Fit1.csv';
    writematrix(BMatrix,fn2);

    y = zeros(1,length(TK));   
    for k = 1:length(DO2mdl.Coefficients.Estimate)
        for j = 1:length(TK)    
                y(1,j) = diffusionModelConcParams(j,k);           
        end
        beta2 = [1.0, 0.5, -0.1];
        xx = TK;
        yy = y(1,:);
        mdlFn2 = @LinearLinear; %@ALinear; %@ConstantLinear; %
        pModel = fitnlm(xx,yy,mdlFn2,beta2);       

        diffusionModelTempParams(k,:) = pModel.Coefficients.Estimate; 

        xxx = TK(1):1.0:TK(end);
        mdlVals = feval(pModel,xxx);

        if k == 1 || k == 2
            if k == 1
                legend_string2 = 'K';
            else
                legend_string2 = 'A''';
            end
            
            figure(k)  
            hold on
            plot(TK,y,'ko','MarkerSize',marker_size,'LineWidth', plot_line_width)       
            plot(xxx,mdlVals,'-b','LineWidth', plot_line_width-1)
            axis square
            box on
            xlabel('Temperature (K)', 'FontSize', axis_label_size,'FontWeight',font_weight)
            ylabel('Parameter value', 'FontSize', axis_label_size,'FontWeight',font_weight)
            
            ax = gca;
            ax.FontSize = tick_label_size;
            ax.FontWeight = font_weight;
            ax.LineWidth = axis_line_width;
            ax.XMinorTick = 'on';
            ax.YMinorTick = 'on';     
            legend(legend_string2, 'Location', 'best')
            legend boxoff            
            hold off            
        elseif k == 3
            mdlVals2 = mdlVals;
            yplot = y;
            continue;
        elseif k == 4
            figure(k)  
            hold on

            yyaxis left
            plot(TK,yplot,'bo','MarkerSize',marker_size,'LineWidth', plot_line_width)                  
            plot(xxx,mdlVals2,'-b','LineWidth', plot_line_width-1)
            ylabel('Parameter value', 'FontSize', axis_label_size,'FontWeight',font_weight)

            yyaxis right
            plot(TK,y,'r+','MarkerSize',marker_size,'LineWidth', plot_line_width)                  
            plot(xxx,mdlVals,'-r','LineWidth', plot_line_width-1)
            ylabel('Parameter value', 'FontSize', axis_label_size,'FontWeight',font_weight)

            axis square
            box on
            xlabel('Temperature (K)', 'FontSize', axis_label_size,'FontWeight',font_weight)
            legend_string2 = {'B''_1','B''_1 fit','B''_2','B''_2 fit'};
            
            ax = gca;
            ax.FontSize = tick_label_size;
            ax.FontWeight = font_weight;
            ax.LineWidth = axis_line_width;
            ax.XMinorTick = 'on';
            ax.YMinorTick = 'on';     
            legend(legend_string2, 'Location', 'best')
            legend boxoff            
            hold off               
        elseif k == 5 
            mdlVals2 = mdlVals;
            yplot = y;
            continue;
        elseif k == 6
            figure(k)  
            hold on

            yyaxis left
            plot(TK,yplot,'bo','MarkerSize',marker_size,'LineWidth', plot_line_width)                  
            plot(xxx,mdlVals2,'-b','LineWidth', plot_line_width-1)
            ylabel('Parameter value', 'FontSize', axis_label_size,'FontWeight',font_weight)

            yyaxis right
            plot(TK,y,'r+','MarkerSize',marker_size,'LineWidth', plot_line_width)                  
            plot(xxx,mdlVals,'-r','LineWidth', plot_line_width-1)
            ylabel('Parameter value', 'FontSize', axis_label_size,'FontWeight',font_weight)

            axis square
            box on
            xlabel('Temperature (K)', 'FontSize', axis_label_size,'FontWeight',font_weight)
            legend_string2 = {'C''_1','C''_1 fit','C''_2','C''_2 fit'};
            
            ax = gca;
            ax.FontSize = tick_label_size;
            ax.FontWeight = font_weight;
            ax.LineWidth = axis_line_width;
            ax.XMinorTick = 'on';
            ax.YMinorTick = 'on';     
            legend(legend_string2, 'Location', 'best')
            legend boxoff            
            hold off               
        end

    end

    writematrix(diffusionModelTempParams,'DiffusionTModel.xlsx')   

    TK_2 = [25.0,25.0,25.0,25.0,25.0,25.0,25.0] + 273.15;
    cCl_2 = [0.0, 1.0, 1.5, 2.0, 3.0, 3.5, 4.0];
    cCl_3 = 0.0:0.01:6.0;
    DO2_2 = [2.11, 1.93, 1.89, 1.87, 1.69, 1.61, 1.55].*1.0e-5; %cm2/s  

    TestTModel = zeros(1,length(pModel.Coefficients.Estimate));
    nCoeffs = length(DO2mdl.Coefficients.Estimate);
    for i = 1:nCoeffs
        TestTModel(1,i) = LinearLinear(diffusionModelTempParams(i,:),TK_2(1));
    end 

    TestCModel = StokesModel2(TestTModel,TK_2(1),cCl_3); 
    CMatrix = [cCl_2',DO2_2'];
    DMatrix = [cCl_3',TestCModel'];
    fn3 = 'vanStroeData.csv';
    writematrix(CMatrix,fn3);
    fn4 = 'vanStroeFit.csv';
    writematrix(DMatrix,fn4);

    figure(20)
    hold on

    plot(cCl_2,DO2_2,'r+','MarkerSize',marker_size,'LineWidth', plot_line_width)
    plot(cCl_3,TestCModel,'-b','LineWidth', plot_line_width)
    
    xlabel('[Cl^-] (M)', 'FontSize', axis_label_size,'FontWeight',font_weight)
    ylabel('Diffusion coefficient (cm^2/s)', 'FontSize', axis_label_size,'FontWeight',font_weight)

    axis square
    box on    
    
    legend_string2 = {'D_{O_2} data','D_{O_2} estimate'};
    ax = gca;
    ax.FontSize = tick_label_size;
    ax.FontWeight = font_weight;
    ax.LineWidth = axis_line_width;
    ax.XMinorTick = 'on';
    ax.YMinorTick = 'on';     
    legend(legend_string2, 'Location', 'best')
    legend boxoff            
    hold off      
    hold off

end

function y = ALinear(b,x)
    y = b(1) + b(2).*x;
end

function y = ConstantLinear(b,x)
    num = b(1);
    denom = 1.0 + b(2).*x;
    y = num./denom;
end

function y = LinearLinear(b,x)
    num = b(1) + b(2).*x;
    denom = 1.0 + b(3).*x;
    y = num./denom;
end

function D = StokesModel(b,idx, c) %,T
    TC_1 = [20.0, 25.0, 30.0, 35.0, 40.0]; %C
    TK = (TC_1 + 273.15);
%     idx = cAll(2,1);

    MH2O = 18.01528; %32.0; % g/mol
    VO2 = 22.414; %L/mol
    VNaCl = 16.6;
    phi = 2.6; %0.2;
    exponent = 0.6;

    eta0 = b(5)*exp(b(6)/TK(idx));
    B = b(3) + b(4).*(TK(idx) - 273.15); %0.01 + 0.06.*(TK(idx)-273.15); %0.080;
    A = b(2); %0.0065;
    eta = eta0*(1.0 + A.*sqrt(c) + B.*c);

    D = b(1).* ((sqrt(phi*MH2O).*TK(idx))./((VO2.*eta).^exponent));
end

function D = StokesModel2(b,TK,c)

    MH2O = 18.01528; %32.0; % g/mol
    VO2 = 22.414; %L/mol
    VNaCl = 16.6;
    phi = 2.6; %0.2;
    exponent = 0.6;

    eta0 = b(5).*exp(b(6)./TK);
    B = b(3) + b(4).*(TK - 273.15); %0.01 + 0.06.*(TK(idx)-273.15); %0.080;
    A = b(2); %0.0065;
    eta = eta0.*(1.0 + A.*sqrt(c) + B.*c);

    D = b(1).* ((sqrt(phi*MH2O).*TK)./((VO2.*eta).^exponent));

end