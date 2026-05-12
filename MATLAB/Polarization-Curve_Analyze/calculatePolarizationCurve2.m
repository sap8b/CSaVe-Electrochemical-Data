function [v,iVals] = calculatePolarizationCurve2(Temperature,cCl,eApp)

    % ==========================
    % Physical constants
    % ==========================
    R = 8.314; %J/(mol K)
    F = 96485.3; %coul/mol    
    kb = 1.38e-23;
    planck_h = 6.626e-34; 
    % ==========================
    % Conversion values
    % ==========================
    E_SHE_to_SCE = 0.244;  
    convertCtoK = 273.15;
    
    M_H2 = 2.016; %g/mol
    M_OH = 17.008; %g/mol
    M_O2 = 32.0; %g/mol
    M_H2O = 18.01528; %g/mol

    M_Cr = 51.9961;
    M_Fe = 55.845;
    M_Ni = 58.6934;

    D_H = 0.00009311; %cm2/sec
    D_H2O = 2.299e-5; %cm2/sec    

    e0_orr_acid = 1.223; %V_SHE
    e0_orr_alk = 0.401;  %V_SHE
    e0_her_alk = -0.83; %V_SHE
    e0_her_acid = 0.0; %V_SHE
    e0_me_ox = 0.0;
    e0_Cr_ox = -0.74; %V_SHE
    e0_Fe_ox = -0.41;  %V_SHE
    e0_Ni_ox = -0.23; %V_SHE    

    z_orr = 4;        
    z_her = 2;
    z_Cr_ox = 3;
    z_Fe_ox = 2;
    z_Ni_ox = 2;     

    cCl = 0.6; %2.7; %0.1; %
    Temperature = 25.0; %10.0; %
    TK = Temperature + convertCtoK;
    pH = 7.0;
    cO2 = Calc_C_O2(TK, cCl); %mol/L
    cH = 10.0^-(pH); %mol/L
    cOH = 10.0^-(14.0-pH); %mol/L
    cH2O = 55.55; %mol/L
    % ==========================
    
    catReactions = {'HER_Alkaline','ORR_Alkaline','ORR_Acid'};
    nCatReactions = length(catReactions);
    
    anReactions = {'Cr_Ox'};
    nAnReactions = length(anReactions);
    
    eApp = dataPot; %0.3:-0.01:-2.0;
    nPots = length(eApp);

    iVals = zeros(nPots,nCatReactions+nAnReactions+1);

    for i = 1:nCatReactions
        nameString = catReactions{i};

        switch nameString
            case 'ORR_Acid'   
                % =====
                % ORR - 4e- acid
                % O2 + 4H+ + 4e- -> 2H2O
                % -- or ---
                % ORR - 2e- alkaline
                % 𝑂_2+𝐻_2 𝑂+2𝑒^−->𝐻𝑂_2^−+𝑂𝐻^−
                % 𝐻𝑂_2^−+𝐻_2 𝑂+2𝑒^−->3𝑂𝐻^−
                % =====                 
                dG_cathodic = calculateParameter(cCl,Temperature,9);
                dG_anodic = calculateParameter(cCl,Temperature,10);
                alpha = calculateParameter(cCl,Temperature,11);
                del(1) = calculateParameter(cCl,Temperature,12); 
                del(2) = calculateParameter(cCl,Temperature,8);

                cReactants(1) = cO2;
                cReactants(2) = cH;    
                c_products(1) = cH2O;

                c_H_g_cm3 = cReactants(2)*M_H2/1000; %g/cm3 
                D1 = D_H;
                z = z_orr;
                iLim1 = -(z*F*D1*cReactants(1))/del(1); %A/cm2    c_H_g_cm3

                D2 = ModelO2DiffusionCoefficient(cCl,TK); %cm2/sec
                iLim2 = -(z*F*D2*cReactants(1))/del(2); %A/cm2
                iLim = iLim1;

                pre_factor = (R*TK)/(z*F); 
                EN_log = log((cReactants(1) * c_H_g_cm3^4)/c_products(1));
                EN = e0_orr_acid + (pre_factor * EN_log);

                eta = eApp - (EN - E_SHE_to_SCE);
                exp_val = -dG_cathodic/(R * TK);
                exp_term = exp(exp_val);
                lambda_0 = (kb*TK)/planck_h; 
                i0_Cathodic = (z*F*lambda_0) * exp_term;       
                
                exp_val = -dG_anodic/(R * TK);

                exp_term = exp(exp_val);
                i0_Anodic = (z*F*lambda_0) * exp_term;   

                iCathodic = -i0_Cathodic.*exp((-(1-alpha)*z*F.*eta)./(R*TK));
                iAnodic = i0_Anodic.*exp((alpha*z*F.*eta)./(R*TK)); 
                iAct = iCathodic + iAnodic;
                iKL = (iLim  .* iAct)./(iAct + iLim);
                
                qRatio = ones(size(eApp));
                sigma1 = 0.02;
                iP = iLim;

                vPeak = CalculateVSiteRxn1(cCl,Temperature); %-0.32;
                dPeak = 0.3;
                endV = vPeak - dPeak;
                q0 = sqrt(2 * pi) * sigma1 * (iP);

                dx = abs(vPeak - endV);
                aLHS = erf(dx /(sqrt(2) * sigma1)); 
                qT = (q0 * aLHS); % Only half the area under the full Gaussian!
        
                for j = 1:length(qRatio)
                    v = eApp(j);
                    if v < endV %indexOfMinCurrent
                        qRatio(j) = 0.0;
                    elseif v >= endV && v <= (vPeak - (dPeak/2.0)) %idxEnd                        
                        dx = abs(vPeak - v);
                        lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                        qVal = (q0/2.0) * lVal;
                        qRatio(j) = qVal/qT;                                            
                    elseif v > (vPeak - (dPeak/2.0)) && v <= vPeak %idxP_L
                        dx = abs(vPeak - v);
                        lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                        qVal = (q0/2.0) * lVal;
                        qRatio(j) = qVal/qT; 
                    elseif v > vPeak && v <= (vPeak + (dPeak/2.0))
                        dx = abs(vPeak - v);
                        lVal = erf(dx/(sqrt(2) * sigma1)); %1 - 
                        qVal = (q0/2.0) * (1.0 + lVal); 
                        qRatio(j) = qVal/qT;                         
                    elseif v > (vPeak + (dPeak/2.0))  
                        dx = abs(vPeak - v);
                        lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                        qVal = (q0/2.0) * lVal;
                        qRatio(j) = 1.0; % qVal/qT;                                            
                    end                                        
                end      
%                 figure(3)
%                 plot(eApp,qRatio,'-b')

                figure(2)
                hold on
                plot(abs(iAct),eApp,'-r')
                plot(abs(iKL),eApp,'-b')
                plot(abs(iKL).*qRatio,eApp,'-k')
                ax = gca;
                ax.XScale = 'log';                
                hold off
                
                iVals(:,i) = iKL.*qRatio;

            case 'ORR_Alkaline'
                % =====
                % ORR - 4e- Alkaline
                % O2 + 2H2O + 4e- -> 4OH- 
                % =====       
                dG_cathodic = calculateParameter(cCl,Temperature,5);
                dG_anodic = calculateParameter(cCl,Temperature,6);
                alpha = calculateParameter(cCl,Temperature,7);
                del = calculateParameter(cCl,Temperature,8); 

                cReactants(1) = cO2;
                cReactants(2) = cH2O;    
                c_products(1) = cOH;

                z = z_orr;
                diffusionLength = del;               
                diffusionCoefficient = ModelO2DiffusionCoefficient(cCl,TK);
         
                c_OH_g_cm3 = c_products(1)*M_OH/1000; %g/cm3   
    
                pre_factor = (R*TK)/(z*F); 
                EN_log = log(cReactants(1)/(c_OH_g_cm3^4));
                EN = e0_orr_alk + (pre_factor * EN_log);
                iLim = -(z*F*diffusionCoefficient*cReactants(1))/diffusionLength; %A/cm2 %ilim_orr = -8.0e-6; %A/cm2

                eta = eApp - (EN - E_SHE_to_SCE);

                exp_val = -dG_cathodic/(R * TK);
                exp_term = exp(exp_val);
                lambda_0 = (kb*TK)/planck_h; 
                i0_Cathodic = (z*F*lambda_0) * exp_term;     

                exp_val = -dG_anodic/(R * TK);
                exp_term = exp(exp_val);
                i0_Anodic = (z*F*lambda_0) * exp_term;   

                iCathodic = -i0_Cathodic.*exp((-(1-alpha)*z*F.*eta)./(R*TK));
                iAnodic = i0_Anodic.*exp((alpha*z*F.*eta)./(R*TK)); 
                iAct = iCathodic + iAnodic;
                iKL = (iLim  .* iAct)./(iAct + iLim);

                qRatio = ones(size(eApp));
                sigma1 = 0.02;
                iP = iLim;
                vPeak = CalculateVSiteORR(cCl,Temperature) ; %eApp(1) - 0.1;
                dPeak = 0.2;
                endV = vPeak + dPeak;
                q0 = sqrt(2 * pi) * sigma1 * (iP);

                dx = abs(vPeak - endV);
                aLHS = erf(dx /(sqrt(2) * sigma1)); 
                qT = (q0 * aLHS); % Only half the area under the full Gaussian!
        
                for j = 1:length(qRatio)
                    v = eApp(j);
                    if v > endV %indexOfMinCurrent
                        qRatio(j) = 0.0;
                    elseif v <= endV && v >= (vPeak + (dPeak/2.0)) %idxEnd                        
                        dx = abs(vPeak - v);
                        lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                        qVal = (q0/2.0) * lVal;
                        qRatio(j) = qVal/qT;                                            
                    elseif v < (vPeak + (dPeak/2.0)) && v >= vPeak %idxP_L
                        dx = abs(vPeak - v);
                        lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                        qVal = (q0/2.0) * lVal;
                        qRatio(j) = qVal/qT; 
                    elseif v < vPeak && v >= (vPeak - (dPeak/2.0))
                        dx = abs(vPeak - v);
                        lVal = erf(dx/(sqrt(2) * sigma1)); %1 - 
                        qVal = (q0/2.0) * (1.0 + lVal); 
                        qRatio(j) = qVal/qT;                         
                    elseif v < (vPeak - (dPeak/2.0))  
                        dx = abs(vPeak - v);
                        lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                        qVal = (q0/2.0) * lVal;
                        qRatio(j) = 1.0; % qVal/qT;                                            
                    end                                        
                end      
%                 figure(4)
%                 plot(eApp,qRatio,'-b')

                figure(5)
                hold on
                plot(abs(iAct),eApp,'-r')
                plot(abs(iKL),eApp,'-b')
                plot(abs(iKL).*qRatio,eApp,'-k')
                ax = gca;
                ax.XScale = 'log';                
                hold off

                iVals(:,i) = iKL.*qRatio;
             
                
            case 'HER_Alkaline'
                % =====    
                % HER
                % 2H+ + 2e- -> H2
                % 2H2O + 2e- -> H2 + 2OH-
                % =====      
                dG_cathodic = calculateParameter(cCl,Temperature,1);
                dG_anodic = calculateParameter(cCl,Temperature,2);
                alpha = calculateParameter(cCl,Temperature,3);
                del = calculateParameter(cCl,Temperature,4); 

                cReactants(1) = cH2O;  
                c_products(1) = cOH; 

                z = z_her;
                diffusionLength = del;
                diffusionCoefficient = D_H2O;
         
                c_H2O_g_cm3 = cReactants(1)*M_H2O/1000; %g/cm3   
                c_OH_g_cm3 = c_products(1)*M_OH/1000; %g/cm3  
    
                pre_factor = (R*TK)/(z*F); 
                EN_log = log(c_H2O_g_cm3/(c_OH_g_cm3^2));
                EN = e0_her_alk + (pre_factor * EN_log);
                iLim = -(z*F*diffusionCoefficient*cReactants(1))/diffusionLength; %A/cm2 %ilim_orr = -8.0e-6; %A/cm2

                eta = eApp - (EN - E_SHE_to_SCE);
                exp_val = -dG_cathodic/(R * TK);
                exp_term = exp(exp_val);
                lambda_0 = (kb*TK)/planck_h; 
                i0_Cathodic = (z*F*lambda_0) * exp_term;

                exp_val = -dG_anodic/(R * TK);
                exp_term = exp(exp_val);
                i0_Anodic = (z*F*lambda_0) * exp_term;   

                iCathodic = -i0_Cathodic.*exp((-(1-alpha)*z*F.*eta)./(R*TK));
                iAnodic = i0_Anodic.*exp((alpha*z*F.*eta)./(R*TK)); 
                iAct = iCathodic + iAnodic;
                iKL = (iLim  .* iAct)./(iAct + iLim);
                if del == 1
                    iKL = iAct;
                end
                iVals(:,i) = iKL;                 

            case 'HER_Acid'
            otherwise
        end
    end
    
    for i = 1:nAnReactions
        nameString = anReactions{i};

        switch nameString
            case 'Cr_Ox'  
                dG_cathodic = calculateParameter(cCl,Temperature,13);
                dG_anodic = calculateParameter(cCl,Temperature,14);
                alpha = calculateParameter(cCl,Temperature,15);
                del = calculateParameter(cCl,Temperature,16); 

                cReactants(1) = 1.0;
                c_products(1) = 1.0e-6;

                z = z_Cr_ox; 
                c_Cr_g_cm3 = c_products(1)*M_Cr/1000; %g/cm3          
                pre_factor = (R*TK)/(z*F); 
                EN_log = log(cReactants(1)/c_Cr_g_cm3);                
                EN = e0_Cr_ox + (pre_factor * EN_log); 
                eta = eApp - (EN - E_SHE_to_SCE);    
    
                lambda_0 = (kb*TK)/planck_h; 
                exp_val = -dG_cathodic/(R * TK);
                exp_term = exp(exp_val);
                i0_Cathodic = (z*F*lambda_0) * exp_term;            
    
                exp_val = -dG_anodic/(R * TK);
                exp_term = exp(exp_val);
                
                i0_Anodic = (z*F*lambda_0) * exp_term;
            
                iCathodic = -i0_Cathodic.*exp((-(1-alpha)*z*F.*eta)./(R*TK));
                iAnodic = i0_Anodic.*exp((alpha*z*F.*eta)./(R*TK));
    
                iAct = iCathodic + iAnodic;
                iKL = iAct; 
%                 figure(3)
%                 plot(iAct,eApp,'-b')
%                 ax = gca;
%                 ax.XScale = 'log';

                iVals(:,i+nCatReactions) = iKL;                 
        end
    end

    for j = 1:nPots
        sumCurrent = 0.0;
        for i = 1:nCatReactions
            sumCurrent = sumCurrent + iVals(j,i); 
        end
        for i = 1:nAnReactions
            sumCurrent = sumCurrent + iVals(j,i+nCatReactions);
        end
        iVals(j,end) = abs(sumCurrent);        
    end

end
function p = calculateParameter(cc,T,j)
    Table = readtable('PolarizationCurveParameters_13.8_pc2.csv','NumHeaderLines',1);
    fit = Table.Fit(j);
    a11 = Table.a11(j);
    a12 = Table.a12(j);
    a13 = Table.a13(j);
    a21 = Table.a21(j);
    a22 = Table.a22(j);
    a23 = Table.a23(j);
    a31 = Table.a31(j);
    a32 = Table.a32(j);
    a33 = Table.a33(j);

    switch fit
        case 1
            A1 = a11 + (a12.*cc) + (a13.*cc.^2);
            A2 = a21 + (a22.*cc) + (a23.*cc.^2);
            A3 = a31 + (a32.*cc) + (a33.*cc.^2);
            p = (A1 + (A2.*T)) ./ (1.0 + A3.*T);
%             p = A1 + (A2.*T) + (A3.*T.^2);
        case 2
            A1 = a11 + (a12.*cc) + (a13.*cc.^2);
            A2 = a21 + (a22.*cc) + (a23.*cc.^2);
            A3 = a31 + (a32.*cc) + (a33.*cc.^2);
%             p = (A1 + (A2.*T)) ./ (1.0 + A3.*T);
            p = A1 + (A2.*T) + (A3.*T.^2);            
        case 3
            A1 = a11 + (a12.*cc) + (a13.*cc.^2);
            A2 = a21 + (a22.*cc) + (a23.*cc.^2);
            A3 = a31 + (a32.*cc) + (a33.*cc.^2);
%             p = A1 + (A2.*T) ./ (1.0 + A3.*T);
            p = A1 + (A2.*T) + (A3.*T.^2);            
        case 4
            A1 = a11 + (a12.*cc) + (a13.*cc.^2);
            A2 = a21 + (a22.*cc) + (a23.*cc.^2);
            A3 = a31 + (a32.*cc) + (a33.*cc.^2);
%             p = A1 + (A2.*T) ./ (1.0 + A3.*T);
            p = A1 + (A2.*T) + (A3.*T.^2);            
        case 5
            A1 = a11 + (a12.*T) ./ (1.0 + a13.*T);
            A2 = a21 + (a22.*T) ./ (1.0 + a23.*T);
            A3 = a31 + (a32.*cc) + (a33.*cc.^2);
%             p = A1 + (A2.*T) ./ (1.0 + A3.*T);
            p = A1 + (A2.*T) + (A3.*T.^2);            
        case 6
            A3 = a13 + (a32.*T) ./ (1.0 + a33.*T);
            A2 = a21 + (a22.*T) ./ (1.0 + a23.*T);
            A1 = a11 + (a12.*cc) + (a13.*cc.^2);
%             p = A1 + (A2.*T) ./ (1.0 + A3.*T);
            p = A1 + (A2.*T) + (A3.*T.^2);                
        case 100
            p = a11;
    end

%     p = (a.*cc.*T) + (b.*T.^2) + (c.*cc.^2) + (d.*T) + (e.*cc) + f;
end
        
function o2_conc = Calc_C_O2(T, Cl)
        convert_g_to_kg = 1.0 / 1000.0;
        molecular_mass_Cl = 35.5 * convert_g_to_kg;
        temperature_k = T; 
        Cl_molality_kg = molecular_mass_Cl * Cl;
        convert_kg_to_mg = 1000.0 * 1000.0;
        Cl_molality_mg = Cl_molality_kg * convert_kg_to_mg;

        a1 = 31820.0;
        b1 = -229.9;
        c1 = -19.12;
        d1 = 0.3081;
        a2 = -1409.0;
        b2 = 10.4;
        c2 = 0.8628;
        d2 = -0.0005235;
        d3 = 0.07464;

        acentric_factor_O2 = 0.022;
        num1 = (a1 * acentric_factor_O2) + a2;
        num2 = (b1 * acentric_factor_O2) + b2;
        denom1 = (c1 * acentric_factor_O2) + c2;
        denom2 = 1 + (denom1 * temperature_k);

        Ln_H_s_0 = (num1 + (num2 * temperature_k)) / denom2;

        num3 = d1 + (d2 * temperature_k);
        denom3 = 1.0 + (d3 * temperature_k);

        salinity_factor = 0.001;
        salinity = salinity_factor * Cl_molality_mg;
        exp_term = num3 / denom3 * salinity;

        Ln_H_s = Ln_H_s_0 + exp_term;

        K_H = exp(Ln_H_s);
        %K_H_conv = K_H * (molecular_mass_o2) * (1.0 / 1.03);% ' (L atm)/mol
        O2_fraction_in_air = 0.209476;
        x1 = O2_fraction_in_air / K_H;
        
        molecular_mass_O2 = 32 * convert_g_to_kg;
        x1_g_L = x1 * molecular_mass_O2 / convert_g_to_kg;
%                 convert_g_to_mg = 1000.0;
%                 x1_mg_L = x1_g_L * convert_g_to_mg;
%                 x1_mol_L = (x1_mg_L/convert_g_to_mg)/(molecular_mass_O2 * convert_g_to_mg); %  #x1_g_L / (molecular_mass_O2 / convert_g_to_kg);
        
        convert_L_to_cm3 = 1000;
        x1_g_cm3 = x1_g_L/convert_L_to_cm3;
        o2_conc = x1_g_cm3;        
end

function i = CalculateCurrent(obj,vApp)
    obj.eta = vApp - (obj.EN - obj.E_SHE_to_SCE);    

    exp_val = -obj.dG_cathodic/(obj.R * obj.Temperature);
    exp_term = exp(exp_val);
    obj.i0 = (obj.z*obj.F*obj.lambda_0) * exp_term;            

    exp_val = -obj.dG_anodic/(obj.R * obj.Temperature);
    exp_term = exp(exp_val);
    obj.i0_Anodic = (obj.z*obj.F*obj.lambda_0) * exp_term;

    if length(obj.alpha) == 1
        obj.iCathodic = -obj.i0.*exp((-(1-obj.alpha)*obj.z*obj.F.*obj.eta)./(obj.R*obj.Temperature));
        obj.iAnodic = obj.i0_Anodic.*exp((obj.alpha*obj.z*obj.F.*obj.eta)./(obj.R*obj.Temperature));
    else
        for i = 1:length(obj.eta)                   
            obj.iCathodic(i) = -obj.i0.*exp((-(1-obj.alpha(i))*obj.z*obj.F.*obj.eta(i))./(obj.R*obj.Temperature));
            obj.iAnodic(i) = obj.i0_Anodic.*exp((obj.alpha(i)*obj.z*obj.F.*obj.eta(i))./(obj.R*obj.Temperature));                    
        end
        
    end

    iAct = obj.iCathodic + obj.iAnodic;
    i = (obj.iLim  .* iAct)./(iAct + obj.iLim );   
end

function iLim = CalculateDiffusionLimitedCurrent(obj, Vapp)
    num = obj.z*obj.F*obj.diffusionCoefficient*obj.cReactants(1);
    iLim = -num/obj.diffusionLength; %A/cm2            
end

function DO2 = ModelO2DiffusionCoefficient(cCl,TK)
    %LoadPolarizationCurve - Loads one or more polarization curves
    %
    %The purpose of this function is to load a polarization curve from
    %the specified file. The file must be a comma separated variable
    %type file.
    %
    % Syntax:  LoadAPolarizationCurve(obj, baseDirectory)
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
    % Author:   Steven A. Policastro, Ph.D., Materials Science
    % Center for Corrosion Science and Engineering, U.S. Naval Research
    % Laboratory
    % email address: steven.policastro@nrl.navy.mil  
    % Website: 
    % October 2021; Last revision: 26-Oct-2021
    %==========================================================================
    
    %------------- BEGIN CODE --------------
    
    MO2 = 32.0; % g/mol
    VO2 = 22.414; %L/mol
    VNaCl = 16.6; 
    phi = 0.2;

    params = [0.193015581, -0.000936823, -3738.145703; ...
        0.586220598, -0.001982362, -0.003767555; ...
        -2058331786, 7380780.538, -725742.0949; ...
        -12341118, 7397.380585, -1024619.196; ...
        -0.082481761, 8.05605E-06, -0.005230993; ...
        -13685.50552, 11.9799009, -0.05822883];

    numModelParameters = size(params,1);

    b = zeros(numModelParameters,1);
    for i = 1:numModelParameters
        b(i,1) = LinearLinear(params(i,:),TK);
    end

    B = b(3,1) + b(4,1).*(TK - 273.15); 
    A = b(2,1); 

    eta2 = b(5,1).*exp(b(6,1)./TK);
    eta1 = 1.0 + A.*sqrt(cCl) + B.*cCl;
    eta = eta2.*eta1;
    
    DO2 = b(1,1).* ((sqrt(phi*MO2).*TK)./((VNaCl.*eta).^0.6));
    
    %------------- END OF CODE --------------
end

function y = LinearLinear(b,x)
    num = b(1) + b(2).*x;
    denom = 1.0 + b(3).*x;
    y = num./denom;
end

function v = CalculateVSiteRxn1(clIn,TIn)
       cl = clIn/2.285;
       T = TIn/26.15;
       a =    0.005103;
       b =   -0.008413;%  (-0.02353, 0.006708)
       c =   -0.008439;%  (-0.02697, 0.01009)
       d =    0.005269;%  (-0.00526, 0.0158)
       e =    0.004872;%  (-0.006299, 0.01604)
       f =     -0.3035;%  (-0.3307, -0.2762)

       v = (a*cl*T) + (b*T^2) + (c*cl^2) + (d*T) + (e*cl) + f;
end

function v = CalculateVSiteORR(clIn, TIn)
       cl = clIn/2.285;
       T = TIn/26.15;

       a =      0.0115; %  (-0.01085, 0.03386)
       b =    0.004796; %  (-0.02272, 0.03231)
       c =     0.00171; %  (-0.03202, 0.03543)
       d =     0.01509; %  (-0.004068, 0.03425)
       e =     0.02927; %  (0.008938, 0.0496)
       f =     -0.2765; %  (-0.3261, -0.2268)

       v = (a*cl*T) + (b*T^2) + (c*cl^2) + (d*T) + (e*cl) + f;
       v = v + 0.15;
end