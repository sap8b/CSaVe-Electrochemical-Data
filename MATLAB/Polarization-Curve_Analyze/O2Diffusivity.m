function O2Diffusivity
    clc;
    clear all;
    
    % Analytica Chimica Acta, Volume 279, Issue 2, 15 July 1993, Pages 213-21
    TC_1 = [20.0, 25.0, 30.0, 35.0, 40.0]; %C
    TK = (TC_1 + 273.15);
    
    cCl_1 = [0.0, 0.0846, 0.154, 0.282, 0.4231, 0.5642, 0.7052, 0.8463]; %M
    
    DO2_1 = [1.98, 1.94, 1.96, 1.95, 1.93, 1.91, 1.96, 1.97; ...
        2.26, 2.20, 2.22, 2.21, 2.12, 2.17, 2.25, 2.24; ...
        2.56, 2.50, 2.47, 2.48, 2.38, 2.47, 2.55, 2.51; ...
        2.96, 2.80, 2.74, 2.71, 2.69, 2.78, 2.88, 2.81; ...
        3.26, 3.15, 3.12, 3.14, 3.00, 3.12, 3.23, 3.13].*1.0e-5; %cm2/s
    
    diffusionModelConcParams = zeros(length(TK),4);
    diffusionModelTempParams = zeros(4,3);

    for i = 1:length(TK)
        if i == 1
            x = cCl_1(3:6);
            y = DO2_1(i,3:6);               
        elseif i == 4
            x = cCl_1(1:3);
            y = DO2_1(i,1:3);            
        else
            x = cCl_1(1:5);
            y = DO2_1(i,1:5);            
        end

        beta = [1.0e-7, 0.065, 0.01, 0.06];
        mdlFn = @StokesModel;

        DO2mdl = fitnlm(x,y,@(beta,x)StokesModel(beta, i, x),beta);
        diffusionModelConcParams(i,:) = DO2mdl.Coefficients.Estimate;

        cPlot = 0.0:0.01:5.2;
        mdlD = feval(DO2mdl,cPlot);
    
        figure(1)
        hold on
        plot(cCl_1,DO2_1(i,:),'ro')
        plot(cPlot, mdlD, '-b')
        hold off
    end

    y = zeros(1,4);    
    for k = 1:4
        for j = 1:length(TK)     
                y(1,j) = diffusionModelConcParams(j,k);            
        end
        beta2 = [1.0, 0.5, -0.1];
        xx = TK;
        yy = y(1,:);
        mdlFn2 = @LinearLinear;
        pModel = fitnlm(xx,yy,mdlFn2,beta2);
        
        diffusionModelTempParams(k,:) = pModel.Coefficients.Estimate;

        xxx = TK(1):1.0:TK(end);
        mdlVals = feval(pModel,xxx);

        figure(k+1)   
        hold on
        plot(TK,y,'ro')        
        plot(xxx,mdlVals,'-b')
        hold off
    end

    writematrix(diffusionModelTempParams,'DiffusionTModel.xlsx')
    
    TK_2 = [25.0,25.0,25.0,25.0,25.0,25.0,25.0] + 273.15;
    cCl_2 = [0.0, 1.0, 1.5, 2.0, 3.0, 3.5, 4.0]; 
    DO2_2 = [2.11, 1.93, 1.89, 1.87, 1.69, 1.61, 1.55].*1.0e-5; %cm2/s
    
    TestTModel = zeros(1,4);
    for i = 1:4
        TestTModel(1,i) = LinearLinear(diffusionModelTempParams(i,:),TK_2(1));
    end

    TestCModel = StokesModel2(TestTModel,TK_2(1),cCl_2);

    figure(20)
    hold on
    plot(cCl_2,DO2_2,'r+')
    plot(cCl_2,TestCModel,'-k')
    hold off
end

function D = StokesModel(b,idx, c) %,T
    TC_1 = [20.0, 25.0, 30.0, 35.0, 40.0]; %C
    TK = (TC_1 + 273.15);
%     idx = cAll(2,1); 

    MO2 = 32.0; % g/mol
    VO2 = 22.414; %L/mol
    VNaCl = 16.6; 
    phi = 0.2;

    B = b(3) + b(4).*(TK(idx) - 273.15); %0.01 + 0.06.*(TK(idx)-273.15); %0.080;
    A = b(2); %0.0065;
    eta = 1.0 + A.*sqrt(c) + B.*c;
    
    D = b(1).* ((sqrt(phi*MO2).*TK(idx))./((VNaCl.*eta).^0.6));
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

function D = StokesModel2(b,TK,c)

    MO2 = 32.0; % g/mol
    VO2 = 22.414; %L/mol
    VNaCl = 16.6; 
    phi = 0.2;

    B = b(3) + b(4).*(TK - 273.15); %0.01 + 0.06.*(TK(idx)-273.15); %0.080;
    A = b(2); %0.0065;
    eta = 1.0 + A.*sqrt(c) + B.*c;
    
    D = b(1).* ((sqrt(phi*MO2).*TK)./((VNaCl.*eta).^0.6));
end