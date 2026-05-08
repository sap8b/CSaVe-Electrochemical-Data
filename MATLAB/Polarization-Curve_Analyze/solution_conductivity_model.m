function solution_conductivity_model
    clc;
    clear all;
    
    % =====================================================================   
    % Plotting variables
    tick_label_size = 20;
    axis_label_size = 24;
    title_label_size = 20;
    plot_line_width = 3;
    axis_line_width = 3;
    marker_size = 10;
    font_weight = 'bold';
    % ===================================================================== 
    
    cl_conc_data_05 = [0.0 0.0654, 0.1307, 0.1960, 0.2614, 0.3267, 0.3923, 0.4575, 0.5227, 0.6533, 0.9147, 1.3069]; %mol/L
    cl_conc_data_25 = [0.0 0.113	0.12452	0.18776	0.26492	0.36091	0.46914	0.60766	0.66232	0.74528	0.86366	1.2312	1.4647	1.8416	2.6739	3.1723	3.8769	4.3281	4.8114]; %mol/L
    cl_conc_data_50 = [0.0 0.009626	0.019176	0.030925	0.04857	0.068474	0.11379	0.14556	0.18299	0.27966	0.37615	0.50448	0.6588	0.80532	1.0007	1.2063	1.4934	1.6744	2.09	2.2154	2.23643	2.5153	2.9739	3.2748	3.9907	4.5035	5.0776]; %mol/L

%     cond_data_25 = [0.92, 1.75, 2.53, 3.27, 3.98, 4.71, 5.37, 6.03, 7.28, 9.65, 12.78]; %S/m 
    cond_data_05 = [1.6607e-10, 0.57, 1.06, 1.57, 2.05, 2.49, 2.94, 3.37, 3.80, 4.60, 6.14, 8.18];%S/m
    cond_data_25 = [5.5006e-10 1.196783	1.31044848	1.91871944	2.63356972	3.49036061	4.4216445	5.5661656	6.0039308	6.65833152	7.56393428	10.2029544	11.74909105	14.04717232	18.30150855	20.33063624	22.57441332	23.64094782	24.48136548];%S/m
    cond_data_50 = [1.7268e-09 0.177975114	0.34583916	0.546166425	0.83836677	1.159196346	1.86331125	2.34118704	2.89087602	4.25838282	5.5640108	7.22718048	9.1336032	10.86135084	13.0531308	15.2343627	18.0805938	19.7612688	23.32022	24.3206612	24.06845966	26.5389303	29.5635399	31.3136376	34.7470249	36.595441	38.1378536];%S/m
    
    
    a_model = @linear_linear_rational;
    b_model = @polynomial_fit;
    
    
    opts_a = statset('TolFun',1e-8);
    beta0 = [-1,2,3];
    
    mdl_25_p = fitnlm(cl_conc_data_25,cond_data_25,b_model,beta0,'Options',opts_a);
    
    mdl_05 = fitnlm(cl_conc_data_05,cond_data_05,a_model,beta0,'Options',opts_a);
    mdl_25 = fitnlm(cl_conc_data_25,cond_data_25,a_model,beta0,'Options',opts_a);
    mdl_50 = fitnlm(cl_conc_data_50,cond_data_50,a_model,beta0,'Options',opts_a);
    
    some_concentrations = 1.0e-6:0.01:5.6; %mol/L
    cond_mdl_05 = feval(mdl_05,some_concentrations);
    cond_mdl_25 = feval(mdl_25,some_concentrations);
    cond_mdl_50 = feval(mdl_50,some_concentrations);

    cond_model_25_p = feval(mdl_25_p,some_concentrations);
    
    
%     figure(1)
%     hold on
%     plot(cl_conc_data,cond_data_25,'bo','MarkerSize',marker_size,'LineWidth', plot_line_width)
%     plot(cl_conc_data,cond_data_05,'r+','MarkerSize',marker_size,'LineWidth', plot_line_width)
%     
% %     plot(some_concentrations,cond_mdl_25,'-b','LineWidth', plot_line_width)  
%     plot(some_concentrations,cond_model_25_p,'--g','LineWidth', plot_line_width)   
% %     plot(some_concentrations,cond_mdl_05,'-r','LineWidth', plot_line_width)
%     
%     xlabel('Cl^- concentration (mol/L)', 'FontSize', axis_label_size,'FontWeight',font_weight)  
%     ylabel('Conductivity (S/m)', 'FontSize', axis_label_size,'FontWeight',font_weight)    
%     axis square
%     box on
%     ax = gca;
% %     ax.XScale = 'log';
%     %         ax.YScale = 'log';
%     ax.FontSize = tick_label_size;
%     ax.FontWeight = font_weight;
%     ax.LineWidth = axis_line_width;
% %     ax.XTick = [1.0e-13,1.0e-12,1.0e-11,1.0e-10,1.0e-9,1.0e-8,1.0e-7,1.0e-6,1.0e-5,1.0e-4,1.0e-3,0.01,0.1];
% %     ax.YTick = -1.4:0.2:0.1;       
%     ax.XMinorTick = 'on';
%     ax.YMinorTick = 'on';
%     legend('T = 25^oC','T = 5^oC','Location','northwest')
%     legend boxoff        
%     
%     hold off
    
%     disp(mdl_25)
%     disp(mdl_25.Coefficients.Estimate(1))
    
    temp_data = [5.0, 25.0, 50.0]; %oC
    a1 = [mdl_05.Coefficients.Estimate(1), mdl_25.Coefficients.Estimate(1), mdl_50.Coefficients.Estimate(1)];
    a2 = [mdl_05.Coefficients.Estimate(2), mdl_25.Coefficients.Estimate(2), mdl_50.Coefficients.Estimate(2)];
    a3 = [mdl_05.Coefficients.Estimate(3), mdl_25.Coefficients.Estimate(3), mdl_50.Coefficients.Estimate(3)];
    
    coeff_model1 = @linear_linear_rational; %@linear_model;
    coeff_model2 = @polynomial_fit;

    opts_b = statset('TolFun',1e-8);        
    beta1 = [-1,2,3];

    mdl_a1 = fitnlm(temp_data,a1,coeff_model2,beta1,'Options',opts_b);
    mdl_a2 = fitnlm(temp_data,a2,coeff_model1,beta1,'Options',opts_b);
    mdl_a3 = fitnlm(temp_data,a3,coeff_model2,beta1,'Options',opts_b);
    
    some_temps = 0.0:0.1:50.0;
    cond_a1 = feval(mdl_a1,some_temps);
    cond_a2 = feval(mdl_a2,some_temps);
    cond_a3 = feval(mdl_a3,some_temps);
    
    figure(2)
    
    subplot(3,1,1)
    hold on
    plot(temp_data,a1,'ko')
    plot(some_temps,cond_a1,'--k')
    hold off
    
    subplot(3,1,2)
    hold on
    plot(temp_data,a2,'k+')
    plot(some_temps,cond_a2,'--k')
    hold off
    
    subplot(3,1,3)
    hold on
    plot(temp_data,a3,'k^')
    plot(some_temps,cond_a3,'--k')
    hold off
    
%     a11 = mdl_a1.Coefficients.Estimate(1);
%     a12 = mdl_a1.Coefficients.Estimate(2);
%     
%     a21 = mdl_a2.Coefficients.Estimate(1);
%     a22 = mdl_a2.Coefficients.Estimate(2);
%     
%     a31 = mdl_a3.Coefficients.Estimate(1);
%     a32 = mdl_a3.Coefficients.Estimate(2);
    
    new_temp_1 = 15.0; %oC
    conc = 1.0e-6:0.01:5.6; %mol/L

    A1 = feval(mdl_a1,new_temp_1);
    A2 = feval(mdl_a2,new_temp_1);
    A3 = feval(mdl_a3,new_temp_1);

    k = (A1 + A2.*conc)./(1.0 + A3.*conc);

%     disp(a11)
%     disp(a12)
%     disp(a21)
%     disp(a22)
%     disp(a31)
%     disp(a32)
    
    figure (3)
    hold on
    plot(cl_conc_data_25,cond_data_25,'bo','MarkerSize',marker_size,'LineWidth', plot_line_width)
    plot(cl_conc_data_50,cond_data_50,'bs','MarkerSize',marker_size,'LineWidth', plot_line_width)
    plot(cl_conc_data_05,cond_data_05,'b+','MarkerSize',marker_size,'LineWidth', plot_line_width)    

    plot(some_concentrations,cond_mdl_05,'--r','LineWidth', plot_line_width)
    plot(some_concentrations,cond_mdl_25,'-r','LineWidth', plot_line_width)  
    plot(some_concentrations,cond_mdl_50,'-.r','LineWidth', plot_line_width)  
    
    plot(conc,k,'-k','LineWidth', plot_line_width)

    xlabel('Cl^- concentration (mol/L)', 'FontSize', axis_label_size,'FontWeight',font_weight)  
    ylabel('Conductivity (S/m)', 'FontSize', axis_label_size,'FontWeight',font_weight)    
    axis square
    box on
    ax = gca;
%     ax.XScale = 'log';
    %         ax.YScale = 'log';
    ax.FontSize = tick_label_size;
    ax.FontWeight = font_weight;
    ax.LineWidth = axis_line_width;
%     ax.XTick = [1.0e-13,1.0e-12,1.0e-11,1.0e-10,1.0e-9,1.0e-8,1.0e-7,1.0e-6,1.0e-5,1.0e-4,1.0e-3,0.01,0.1];
%     ax.YTick = -1.4:0.2:0.1;       
    ax.XMinorTick = 'on';
    ax.YMinorTick = 'on';
%     legend('T = 25^oC','T = 5^oC','Model at T = 25^oC','Model at T = 5^oC','Model at T = 15^oC','Location','northwest')
    legend boxoff         
    hold off
end
function y = linear_linear_rational(b,x)
    y = (b(1) + b(2).*x(:,1))./(1 + b(3).*x(:,1));
end
function y = polynomial_fit(b,x)
    y = b(1) + b(2).*x(:,1) + b(3).*x(:,1).^2;
end
function y = linear_model(b,x)
    y = b(1) + b(2).*x(:,1);
end