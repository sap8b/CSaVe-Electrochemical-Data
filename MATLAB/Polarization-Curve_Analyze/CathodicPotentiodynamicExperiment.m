classdef CathodicPotentiodynamicExperiment
    %CathodicPotentiodynamicExperiment - Class for storing and and analyzing polarization
    %data
    %
    %The purpose of this class is to create an object that stores, plots,
    %and analyze polarization data from cathodic polarization curves.
    %
    %==========================================================================
    % Author:   Steve Policastro, Ph.D., Materials Science
    % Center for Corrosion Science and Engineering, U.S. Naval Research
    % Laboratory
    % email address: steven.policastro@nrl.navy.mil  
    % Website: 
    % October 2021; Last revision: 27-Oct-22
    %==========================================================================
        
   properties (SetAccess = private)
        tick_label_size = 16;
        axis_label_size = 18;
        title_label_size = 20;
        axis_line_width = 3;
        font_weight = 'bold';
        plot_line_width = 3;
        plot_line_width_2 = 2;
        outOfRangeVal = -1000;
   end    
   properties
        Name 
        solution NaClSolution
        fitFlag int8
        currentData double
        filteredCurrentData double
        basePotentialData double
        potentialData double
        currentDataAdjusted double
        indicesForFitRegions int32
        currentRangesData_CathodicRxns double
        potentialRangesData_CathodicRxns double
        currentRangesData_AnodicRxns double
        potentialRangesData_AnodicRxns double     
        polCurveFirstDerivativeC double 
        polCurveSecondDerivativeC double 
        polCurveThirdDerivativeC double 
        polCurveFirstDerivativeA double 
        polCurveSecondDerivativeA double         
        exposedArea double
        R_solution double
        allCatRxnsModel ElectrochemicalReductionReaction
        inclCatRxnsModel reactionNames
        allAnRxnsModel ElectrochemicalOxidationReaction
        inclAnRxnsModel reactionNames
        eAppModel double
        iTotModel double
        GoodCurve int8
        numCatReactions int32
        numAnReactions int32
        catReactsDict
        anReactsDict
   end
 
   methods

        function obj = CathodicPotentiodynamicExperiment(fName, fidata, idata, pdata, rawpdata, goodCurve, Temp, cCl, R, pH, nCathodicRxns, nAnodicRxns)
            %CathodicPotentiodynamicExperiment - Constructor method for
            %this class.
            %
            %The purpose of this method is to construct an instance of the
            %CathodicPotentiodynamicExperiment class.  The constructor
            %requires:
            % =============================================
            % Name                              = char array
            % Filtered current data             = numeric array (A/cm^2)
            % Raw current data                  = numeric array (A/cm^2)
            % iR-corrected potential data       = numeric array (V_{SCE})
            % Raw potential data                = numeric array (V_{SCE})
            % Check to see if the data is good  = boolean value
            % Temperature                       = numeric value (^oC)
            % Chloride concentration            = numeric value ([M])
            % Solution resistance               = numeric value (\Ohms)
            % pH                                = numeric value
            % Number of cathodic reactions      = integer value, n <= 3
            % Number of anodic reactions        = integer value, n <= 3
            % =============================================
            % The constructor returns an instance of the class
            % =============================================
            obj.Name = fName;  
            obj.R_solution = R;
            obj.GoodCurve = true;          
            obj.filteredCurrentData = fidata;
            obj.currentData = idata;
            obj.potentialData = pdata;
            obj.basePotentialData = rawpdata;
            obj.GoodCurve = goodCurve;
            % This flag value is adjusted later to determine what reactions
            % have been identified for fitting
            obj.fitFlag = 0;
            % =============================================
            % Setup the applied potential vector
            % =============================================       
            obj.eAppModel = obj.potentialData;    
            obj.solution = NaClSolution(cCl,Temp);
            % =============================================
            % Create the electrochemical reactions
            % ============================================= 
            % Create dictionaries of the reactions
            availCatReacts = [reactionNames.Fe_Red,reactionNames.ORR,reactionNames.HER,reactionNames.None];
            arrayLocation = [1,2,3,4];
            obj.catReactsDict = dictionary(availCatReacts,arrayLocation);

            availAnReacts = [reactionNames.Cr_Ox,reactionNames.Fe_Ox,reactionNames.Ni_Ox,reactionNames.None];
            arrayLocation = [1,2,3,4];
            obj.anReactsDict = dictionary(availAnReacts,arrayLocation);

            C = Constants;
            [cOH, cH] = Constants.calculatecHandcOH(pH);
            % =====
            % Fe reduction 
            % Fe2O3 + 6H+ + 2e- -> 2Fe2+ + 3H2O            
            % =====
            cReact = [1.0, ((cH/1000)*C.M_H2/2.0)^6]; %g/cm3 e-2/1000
            cProd = [(1.0e-6/1000)*C.M_Fe, ((obj.solution.aW/(1000))*C.M_H2O)^3]; %g/cm3
            dg = [1.7e5, 30.0e5];
            b = 0.5;
            del = 2.0e-7; %0.85;   
            pS = '-c';
            Dcoeff = C.D_Fe; % C.D_H;
            obj.allCatRxnsModel(1) = ElectrochemicalReductionReaction(reactionNames.Fe_Red, pS, Temp, cReact, cProd,  dg, b, del, C.z_Fe_red, C.e0_Fe_ox, Dcoeff);
            
            % =====
            % ORR - 4e- Alkaline
            % O2 + 2H2O + 4e- -> 4OH- 
            % =====
            % ORR - 4e- acid
            % =====
            % O2 + 4H+ + 4e- -> 2H2O    
            % =====
            % ORR - 2e- alkaline
            % =====
            % O2 + H2O + 2e- -> HO2- + OH-
            % HO2- + H2O + 2d- -> 3OH-
            % =====    
            cReact = [obj.solution.cO2,((obj.solution.aW/(1000))*C.M_H2O)^2]; %g/cm3
            cProd = [1.0, ((cOH/1000)*C.M_OH)^4]; %g/cm3
            dg = [1.3e5, 80.0e5];
            b = 0.89;
            del = 0.85;
            pS = '-b';
            Dcoeff = obj.solution.dO2;
            obj.allCatRxnsModel(2) = ElectrochemicalReductionReaction(reactionNames.ORR,  pS, Temp, cReact, cProd, dg, b, del, C.z_orr, C.e0_orr_alk, Dcoeff);   
            
            % =====    
            % HER
            % 2H+ + 2e- -> H2
            % 2H2O + 2e- -> H2 + 2OH-
            % =====   
            cReact = [((obj.solution.aW/(1000))*C.M_H2O)^2, 1.0]; %g/cm3 55.55;
            cProd = [1.0, ((cOH/1000)*C.M_OH)^2]; %g/cm3 10.0^-(14.0-pH); %mol/L
            dg = [8.0e4, 100.0e5];
            b = 0.8;
            del = 1.5;   
            pS = '-g';
            Dcoeff = C.D_H2O;
            obj.allCatRxnsModel(3) = ElectrochemicalReductionReaction(reactionNames.HER, pS, Temp, cReact, cProd, dg, b, del, C.z_her, C.e0_her_alk, Dcoeff);  

            % =====    
            % None
            % ===== 
            obj.allCatRxnsModel(4) = ElectrochemicalReductionReaction(reactionNames.None, pS, Temp, cReact, cProd, dg, b, del, C.z_her, C.e0_her_alk, Dcoeff);
            
            % =====    
            % Cr oxidation
            % Cr -> Cr2+  + 3e-
            % ===== 
            cReact = 1.0;
            cProd = 1.0e-6;
            dg = [4.0e5, 1.68e5];
            b = 0.21;
            pS = ':r';
            obj.allAnRxnsModel(1) = ElectrochemicalOxidationReaction(reactionNames.Cr_Ox, pS, Temp, cReact, cProd, obj.eAppModel, dg, b);

            % =====    
            % Fe oxidation
            % Fe -> Fe2+  + 2e-
            % ===== 
            cReact = 1.0;
            cProd = 1.0e-6;
            dg = [40.0e5, 10.68e5];
            b = 0.21;
            pS = ':r';
            obj.allAnRxnsModel(2) = ElectrochemicalOxidationReaction(reactionNames.Fe_Ox, pS, Temp, cReact, cProd, obj.eAppModel, dg, b);

            % =====    
            % Ni oxidation
            % Ni -> Ni2+  + 2e-
            % ===== 
            cReact = 1.0;
            cProd = 1.0e-6;
            dg = [40.0e5, 10.68e5];
            b = 0.21;
            pS = ':r';
            obj.allAnRxnsModel(3) = ElectrochemicalOxidationReaction(reactionNames.Ni_Ox, pS, Temp, cReact, cProd, obj.eAppModel, dg, b);

            obj.inclCatRxnsModel = nCathodicRxns;
            obj.numCatReactions = length(nCathodicRxns);
            obj.iTotModel = zeros(size(obj.eAppModel));

            obj.inclAnRxnsModel = nAnodicRxns;
            obj.numAnReactions = length(nAnodicRxns);

        end

        function Plot_Polarization_Data(obj,figNum)
            h1 = figure(figNum);
        %     set(h1,'Position', [10 10 1200 1200])
            hold on
            plot(obj.filteredCurrentData,obj.potentialData,'-k','LineWidth', obj.plot_line_width-2)
            
            axis square
            box on
            ylim([-1.4,0.0])
            xlim([1.0e-13,0.01])
            xlabel('Current density (A/cm^2)', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
            ylabel('Potential (V_{SCE})', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
            
            ax = gca;
            ax.XScale = 'log';
            ax.FontSize = obj.tick_label_size;
            ax.FontWeight = obj.font_weight;
            ax.LineWidth = obj.axis_line_width;
            ax.XTick = [1.0e-13,1.0e-12,1.0e-11,1.0e-10,1.0e-9,1.0e-8,1.0e-7,1.0e-6,1.0e-5,1.0e-4,1.0e-3,0.01,0.1];
            ax.YTick = -1.4:0.2:0.1;       
            ax.XMinorTick = 'on';
            ax.YMinorTick = 'on';

            legend boxoff
        %     exportgraphics(ax,strcat(Plot_name,'.png'),'Resolution',300)
            hold off 
        end        
        
        function Plot_Polarization_Data_Analysis(obj,currentVals,figNum)
%             max1D = max(abs(obj.polCurveFirstDerivative));
%             [max2D, idxm2d] = max(abs(obj.polCurveSecondDerivative));

%             deleteThese = (abs(obj.polCurveSecondDerivative) >= 100);
%             ValuesWithDeletions = obj.polCurveSecondDerivative(~deleteThese);
%             CurrentWithDeletions = currentVals(~deleteThese);            

%             deleteThese2 = (abs(obj.polCurveFirstDerivative) >= 5);
%             ValuesWithDeletions2 = obj.polCurveFirstDerivative(~deleteThese2);
%             CurrentWithDeletions2 = currentVals(~deleteThese2); 

            figure(figNum)
            hold on
            axis square
            box on

            ax1 = gca;
%             ax1.XScale = 'log';
            
            yyaxis left
%             plot(abs(currentVals), obj.potentialData,'-b','LineWidth', obj.plot_line_width) %./max1D
            plot(obj.polCurveSecondDerivativeC,'-b','LineWidth', obj.plot_line_width) %abs(currentVals),
%             ylabel('Potential (V_{SCE})', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
            ylabel('\partial^2 V/(\partial log(i))^2', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
%             ax1.YTick = -1.3:0.2:0.1;  

            yyaxis right
            plot(obj.polCurveFirstDerivativeC,'-r','LineWidth', obj.plot_line_width) %./max1D abs(currentVals),
            ylabel('Scaled differentiation', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
%             xlabel('Current density (A/cm^2)', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
%             ax1.YTick = -1.0:0.2:1.0;  

%             legend('Average polarization response', '|dV/dlog(i)|/|dV/dlog(i)|_{max}')
            legend boxoff
%             ylabel('\partial V/(\partial log(i))', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
%             plot(abs(obj.filteredCurrentData), obj.potentialData,'-b','LineWidth', obj.plot_line_width)
%             plot(abs(currentVals),obj.polCurveSecondDerivativeC,'-r','LineWidth', obj.plot_line_width)
%             plot(obj.polCurveThirdDerivativeC,'-g','LineWidth', obj.plot_line_width)

%             ylabel('\partial^2 V/(\partial log(i))^2', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
            

                        
%             plot(abs(obj.polCurveFirstDerivative),'-r','LineWidth', obj.plot_line_width) %./max1D
            

%             plot(abs(CurrentWithDeletions), ValuesWithDeletions,'-g','LineWidth', obj.plot_line_width)  %      ./max2D     
            
%             
%             ylim([-5.0e-4 5.0e-4])
%             ax1.YScale = 'log';

            ax1.FontSize = obj.tick_label_size;
            ax1.FontWeight = obj.font_weight;
            ax1.LineWidth = obj.axis_line_width;
%             ax1.XTick = [1.0e-11,1.0e-10,1.0e-9,1.0e-8,1.0e-7,1.0e-6,1.0e-5,1.0e-4,1.0e-3,0.01];
%             ax1.YTick = -1.3:0.2:0.1;    
            ax1.XMinorTick = 'on';
            ax1.YMinorTick = 'on';  

%             ax2 = axes('position', ax1.Position);
%             ax2.XScale = 'log';
%             ax2.YScale = 'log';
%             plot(ax2,abs(currentVals), obj.polCurveFirstDerivative(1,:),'-g')
% %             pause(0.1)                 % see [3]
%             ax2.Color = 'none';   
%             
            hold off

%             figure(figNum+1)
%             hold on
%             plot(abs(obj.filteredCurrentData),'-k','LineWidth', obj.plot_line_width) %, obj.potentialData
%             plot(abs(obj.currentData),'-g','LineWidth', obj.plot_line_width) %, obj.potentialData
%             hold off
        end
    
    end
        % =============================================        
    % These are methods for fitting equations to polarization curves
    methods (Static)

        function vals = FitCurve(obj, aNum, vSite1, vSite2, vSite3, aD)
            numCatRxns = obj.numCatReactions;
            numAnRxns = obj.numAnReactions;            
            vals = zeros(numCatRxns + numAnRxns, 4);
            
            for i = 1:numCatRxns
                rxnName = obj.inclCatRxnsModel(i);
                idxRxn = obj.catReactsDict(rxnName);

                switch rxnName
                    case reactionNames.Fe_Red

                        if (obj.fitFlag == 2 || obj.fitFlag == 1) && obj.currentRangesData_CathodicRxns(1,nR) > obj.outOfRangeVal
                            %===============================================
                            % iLim fit
                            %===============================================
                            nR = 2;
                            fT = 3;
                            [obj.allCatRxnsModel(idxRxn).iLim, obj.allCatRxnsModel(idxRxn).diffusionLength] = CathodicPotentiodynamicExperiment.getiLim(obj, nR, fT, idxRxn);
                        else
                            % If the data set does not contain information
                            % about the diffusion-limited current for a
                            % hydrogen-evolution reaction
                            obj.allCatRxnsModel(idxRxn).iLim = ElectrochemicalReductionReaction.GetDiffusionLimitedCurrent(obj.allCatRxnsModel(idxRxn));                             
                        end  

                        %===============================================
                        % iAct fit
                        %===============================================
                        nR = 1;
                        if obj.currentRangesData_CathodicRxns(1,nR) > obj.outOfRangeVal
                            fFlag = obj.fitFlag;
                            [obj.allCatRxnsModel(idxRxn).dG_cathodic,...
                                obj.allCatRxnsModel(idxRxn).z, ...
                                obj.allCatRxnsModel(idxRxn).alpha, ...
                                obj.fitFlag] = CathodicPotentiodynamicExperiment.getiActParams(obj,nR,obj.allCatRxnsModel(idxRxn).iLim,idxRxn,fFlag);

%                             obj.allCatRxnsModel(idxRxn).iLim = ElectrochemicalReductionReaction.GetDiffusionLimitedCurrent(obj.allCatRxnsModel(idxRxn));
                            obj.allCatRxnsModel(idxRxn).iAct = ElectrochemicalReductionReaction.GetActivationCurrent(obj.allCatRxnsModel(idxRxn),obj.potentialData);
                            newCurrent = ElectrochemicalReductionReaction.GetKouteckyLevich(obj.allCatRxnsModel(idxRxn));  
                        else
                             newCurrent = ones(size(obj.potentialData));
                             newCurrent(1:end) = obj.outOfRangeVal;
                        end        

                        % =============================
                        % Fitting Gaussian Curves
                        % =============================   
                        % Assume high cathodic potentials reduce oxide
                        % =============================                        
                        N = length(newCurrent);
                        qRatio = CathodicPotentiodynamicExperiment.getQ(obj,idxRxn,N,vSite1,vSite2,vSite3);     
                        obj.allCatRxnsModel(idxRxn).qRatio = qRatio;
                        obj.allCatRxnsModel(idxRxn).i = newCurrent.*qRatio;
                    
                        figure(800)
                        hold on
                        plot(abs(obj.filteredCurrentData), obj.potentialData ,'--k')
                        plot(abs(newCurrent),obj.potentialData,'--r')
                        plot(abs(obj.allCatRxnsModel(idxRxn).i),obj.potentialData,'-b')
                        ax = gca;
                        ax.XScale = 'log';
                        hold off

                    case reactionNames.ORR
                        %===============================================
                        % iLim fit
                        %===============================================
                        nR = 4;
                        if obj.fitFlag == 1 && obj.currentRangesData_CathodicRxns(1,nR) > -1
                            fT = 3;
                            [obj.allCatRxnsModel(idxRxn).iLim, obj.allCatRxnsModel(idxRxn).diffusionLength] = CathodicPotentiodynamicExperiment.getiLim(obj, nR, fT, idxRxn);
                        else
                            % If the data set does not contain information
                            % about the diffusion-limited current for a
                            % hydrogen-evolution reaction
                            obj.allCatRxnsModel(idxRxn).iLim = ElectrochemicalReductionReaction.GetDiffusionLimitedCurrent(obj.allCatRxnsModel(idxRxn));                             
                        end                        
                        %===============================================
                        % iAct fit
                        %===============================================
                        nR = 3;
                        if obj.currentRangesData_CathodicRxns(1,nR) > -1
                            fFlag = obj.fitFlag;
                            [obj.allCatRxnsModel(idxRxn).dG_cathodic,...
                                obj.allCatRxnsModel(idxRxn).z, ...
                                obj.allCatRxnsModel(idxRxn).alpha, ...
                                obj.fitFlag] = CathodicPotentiodynamicExperiment.getiActParams(obj,nR,obj.allCatRxnsModel(idxRxn).iLim,idxRxn,fFlag);

%                             obj.allCatRxnsModel(idxRxn).iLim = ElectrochemicalReductionReaction.GetDiffusionLimitedCurrent(obj.allCatRxnsModel(idxRxn));
                            obj.allCatRxnsModel(idxRxn).iAct = ElectrochemicalReductionReaction.GetActivationCurrent(obj.allCatRxnsModel(idxRxn),obj.potentialData);
                            newCurrent = ElectrochemicalReductionReaction.GetKouteckyLevich(obj.allCatRxnsModel(idxRxn));  
                        else
                             newCurrent = ones(size(obj.potentialData));
                             newCurrent(1:end) = obj.outOfRangeVal;
                        end

                        % =============================   
                        % Assume that, at lower cathodic potentials,
                        % the reactions catalyzed on the oxide are more
                        % facile...
                        % =============================                               
                        N = length(newCurrent);
                        qRatio = CathodicPotentiodynamicExperiment.getQ(obj,idxRxn,N,vSite1,vSite2,vSite3);  
                        obj.allCatRxnsModel(idxRxn).qRatio = qRatio;
                        obj.allCatRxnsModel(idxRxn).i = newCurrent.*qRatio; 

                        figure(800)
                        hold on
                        plot(abs(obj.filteredCurrentData), obj.potentialData ,'--k')
                        plot(abs(newCurrent),obj.potentialData,'--r')
                        plot(abs(obj.allCatRxnsModel(idxRxn).i),obj.potentialData,'-b')
                        ax = gca;
                        ax.XScale = 'log';
                        hold off

                    case reactionNames.HER
                        %===============================================
                        % iLim fit
                        %===============================================
                        nR = 6;
                        if obj.currentRangesData_CathodicRxns(1,nR) > -1                            
                            fT = 1;
                            [obj.allCatRxnsModel(idxRxn).iLim, obj.allCatRxnsModel(idxRxn).diffusionLength] = CathodicPotentiodynamicExperiment.getiLim(obj, nR, fT, idxRxn);
                        else
                            % If the data set does not contain information
                            % about the diffusion-limited current for a
                            % hydrogen-evolution reaction
                            obj.allCatRxnsModel(idxRxn).iLim = ElectrochemicalReductionReaction.GetDiffusionLimitedCurrent(obj.allCatRxnsModel(idxRxn)); 
                        end

                        %===============================================
                        % iAct fit
                        %===============================================
                        nR = 5;
                        if obj.currentRangesData_CathodicRxns(1,nR) > -1
                            fFlag = 0;
                            [obj.allCatRxnsModel(idxRxn).dG_cathodic,...
                                obj.allCatRxnsModel(idxRxn).z, ...
                                obj.allCatRxnsModel(idxRxn).alpha, ...
                                obj.fitFlag] = CathodicPotentiodynamicExperiment.getiActParams(obj,nR,obj.allCatRxnsModel(idxRxn).iLim,idxRxn,fFlag);

                            obj.allCatRxnsModel(idxRxn).iAct = ElectrochemicalReductionReaction.GetActivationCurrent(obj.allCatRxnsModel(idxRxn),obj.potentialData);
                            newCurrent = ElectrochemicalReductionReaction.GetKouteckyLevich(obj.allCatRxnsModel(idxRxn));                                               
                        else
                             newCurrent = ones(size(obj.potentialData));
                             newCurrent(1:end) = obj.outOfRangeVal;
                        end

                        qRatio = CathodicPotentiodynamicExperiment.getQ(obj,idxRxn,1,vSite1,vSite2,vSite3);
                        obj.allCatRxnsModel(idxRxn).qRatio = qRatio;
                        obj.allCatRxnsModel(idxRxn).i = newCurrent.*qRatio; 
                        
                        figure(800)
                        hold on
                        plot(abs(obj.filteredCurrentData), obj.potentialData ,'--k')
                        plot(abs(newCurrent),obj.potentialData,'--r')
                        plot(abs(obj.allCatRxnsModel(idxRxn).i),obj.potentialData,'-b')
                        ax = gca;
                        ax.XScale = 'log';
                        hold off

                    case reactionNames.None
                        newCurrent = zeros(size(obj.potentialData));
                        obj.allCatRxnsModel(idxRxn).i = newCurrent;

                    otherwise
                        newCurrent = zeros(size(obj.potentialData));
                        obj.allCatRxnsModel(idxRxn).i = newCurrent;
                end
            end

            for i = 1:numAnRxns
                rxnName = obj.inclAnRxnsModel(i);
                idxRxn = obj.anReactsDict(rxnName); 

                switch obj.inclAnRxnsModel(idxRxn)
                    case reactionNames.Cr_Ox %'Cr_Oxidation'
%                         disp(obj.anRxnsModel(i).name)
                        nzData = nonzeros(obj.currentRangesData_AnodicRxns(:,1));
                        sectionofData = round(length(nzData)/2.0);
                        currents_data1 =  obj.filteredCurrentData(nzData(1:sectionofData));
                        potentials_data1 = obj.potentialRangesData_AnodicRxns(1:sectionofData,1);


                        % Get ORR activation or Rxn 1 activation current
                        idxRxn2 = obj.catReactsDict(reactionNames.ORR);
                        currValsFromORR_act = obj.allCatRxnsModel(idxRxn2).i(nonzeros(obj.currentRangesData_AnodicRxns(:,1))); %ElectrochemicalReductionReaction.ActivationCurrent(obj.catRxnsModel(2),potentials_data1);
                        idxRxn2 = obj.catReactsDict(reactionNames.Fe_Red);
                        currValsFromRxn_act = obj.allCatRxnsModel(idxRxn2).i(nonzeros(obj.currentRangesData_AnodicRxns(:,1)));

                        iAct1 = currValsFromORR_act(1);
                        iAct2 = currValsFromRxn_act(1);
                        if iAct1 > -1 && iAct2 > -1
                            if abs(iAct1) >= abs(iAct2)
                                currValsAct = currValsFromORR_act(1:sectionofData);
                            else
                                currValsAct = currValsFromRxn_act(1:sectionofData);
                            end
                        elseif iAct1 == -1 && iAct2 > -1
                            currValsAct = currValsFromRxn_act(1:sectionofData);
                        elseif iAct1 > -1 && iAct2 == -1
                            currValsAct = currValsFromORR_act(1:sectionofData);
                        end
                        
%                         adj_current_data = currents_data1 + abs(currValsAct);
                        if abs(currValsAct(1)) < currents_data1(1)
                             adj_current_data = currents_data1 + abs(currValsAct); %nonzeros(currents_data1);currValsFromORR_act
                        else
                            adj_current_data = currents_data1;
                        end                       
% %                         adj_potential_data = potentials_data1(1:length(adj_current_data));

                        param_vals= ElectrochemicalOxidationReaction.LM_Fit_Activation(obj.allAnRxnsModel(idxRxn), adj_current_data, potentials_data1); %adj_potential_data
                        obj.allAnRxnsModel(idxRxn).dG_anodic = param_vals(1);
                        obj.allAnRxnsModel(idxRxn).alpha = param_vals(2); 
                        newCurrent2 = obj.allAnRxnsModel(idxRxn).CalculateCurrent(obj.potentialData); %obj.eAppModel  + 0.015
                        
%                         figure(205)
%                         hold on
%                         plot(currents_data1,potentials_data1,'-b')
%                         plot(abs(currValsAct),potentials_data1,'-r')
%                         plot(adj_current_data,potentials_data1,'-g')
%                         plot(newCurrent2(1:length(potentials_data1)),potentials_data1,'-k')
%                         ax = gca;
%                         ax.XScale = 'log';
%                         hold off

                        obj.allAnRxnsModel(idxRxn).i = newCurrent2; 
                        obj.allAnRxnsModel(idxRxn).eApp = obj.potentialData; %obj.eAppModel; %potentials_data1;

                    case reactionNames.Fe_Ox %'Fe_Oxidation'
                    case reactionNames.Ni_Ox %'Ni_Oxidation'
                    otherwise
                end
            end
            
            if obj.allCatRxnsModel(1).i(1) > obj.outOfRangeVal
                obj.iTotModel = obj.allCatRxnsModel(1).i;
                for i = 2:numCatRxns
                    if obj.allCatRxnsModel(i).i(i) > obj.outOfRangeVal
                        obj.iTotModel =  obj.iTotModel + obj.allCatRxnsModel(i).i;
                    end
                end   
            else
                obj.iTotModel = obj.allCatRxnsModel(2).i;
                for i = 3:numCatRxns
                    if obj.allCatRxnsModel(i).i(i) > obj.outOfRangeVal
                        obj.iTotModel =  obj.iTotModel + obj.allCatRxnsModel(i).i;
                    end
                end                 
            end
            for i = 1:numAnRxns
                obj.iTotModel =  obj.iTotModel + obj.allAnRxnsModel(i).i;
            end               

            plotExcessData = false; %true;
            if plotExcessData == false
                CathodicPotentiodynamicExperiment.Plot_Polarization_Data_and_Model(obj,aNum,aD);           
            else
                fn1 = 'C:\Users\steve\OneDrive\Atmospheric Corrosion\Presentations\FY22\ConvertedFiles\CR 100NM POL CURVE 04_pd.csv';
                fn2 = 'C:\Users\steve\OneDrive\Atmospheric Corrosion\Presentations\FY22\ConvertedFiles\FE ON SI 1 H OCV POL CURVE 05_pd.csv';
                fn3 = 'C:\Users\steve\OneDrive\Atmospheric Corrosion\Presentations\FY22\ConvertedFiles\FECR DEP 3 ON SI POL CURVE 12_pd.csv';                
                CathodicPotentiodynamicExperiment.Plot_Polarization_Data_and_Model_OtherFiles(obj,aNum,fn1,fn2,fn3)
            end

            for i = 1:numCatRxns
                vals(i,:) = [obj.allCatRxnsModel(i).dG_cathodic,obj.allCatRxnsModel(i).dG_anodic,obj.allCatRxnsModel(i).alpha(1),obj.allCatRxnsModel(i).diffusionLength];
            end
            
            for i = 1:numAnRxns
                vals(i+numCatRxns,:) = [obj.allAnRxnsModel(i).dG_cathodic,obj.allAnRxnsModel(i).dG_anodic,obj.allAnRxnsModel(i).alpha,obj.allAnRxnsModel(i).diffusionLength];
            end
            
        end

        function [iLim,diffL] = getiLim(obj, numRange, fitType, idxRxn)
            potVals = obj.potentialRangesData_CathodicRxns(:,numRange);
            currVals2 = obj.filteredCurrentData(obj.currentRangesData_CathodicRxns(:,numRange));

            switch obj.allCatRxnsModel(idxRxn).name
                case reactionNames.HER
                    idxRxn2 = obj.catReactsDict(reactionNames.None);
                    [iact,~,~,~] = ElectrochemicalReductionReaction.GetActivationCurrent(obj.allCatRxnsModel(idxRxn2),potVals);
                case reactionNames.ORR
                    idxRxn2 = obj.catReactsDict(reactionNames.HER);
                    iact = obj.allCatRxnsModel(idxRxn2).i(obj.currentRangesData_CathodicRxns(:,numRange));
                case reactionNames.Fe_Red   
                    idxRxn2 = obj.catReactsDict(reactionNames.ORR);  
                    iact = obj.allCatRxnsModel(idxRxn2).i(obj.currentRangesData_CathodicRxns(:,numRange));
                otherwise
            end
            
            currVals = currVals2 - abs(iact);

            diffL = ElectrochemicalReductionReaction.LM_Fit_Transport(obj.allCatRxnsModel(idxRxn),currVals, potVals,fitType);
            obj.allCatRxnsModel(idxRxn).diffusionLength = diffL;
            iLim = ElectrochemicalReductionReaction.GetDiffusionLimitedCurrent(obj.allCatRxnsModel(idxRxn));

%             figure(403)
%             hold on
%             plot(abs(currVals2),potVals, ':k')
%             plot(abs(iact),potVals, '-.r')
%             plot(abs(currVals),potVals, '--b')
%             ax = gca;
%             ax.XScale = 'log';
%             xlabel('current density (A/cm^2)')
%             ylabel('potential (V_{SCE})')
%             hold off
        end

        function [dG_cathodic,z,alpha,fF] = getiActParams(obj,nR,iL,idxRxn,fFlag)
            potVals = obj.potentialRangesData_CathodicRxns(:,nR);
            currVals2 = obj.filteredCurrentData(obj.currentRangesData_CathodicRxns(:,nR));
            currVals = zeros(size(currVals2));

            for k = 1:length(currVals2)
                currVals(k) = -ElectrochemicalReductionReaction.NR_ia(currVals2(k),abs(iL),currVals2(k));
            end
            
            switch obj.allCatRxnsModel(idxRxn).name
                case reactionNames.HER 
                    idxRxn2 = obj.catReactsDict(reactionNames.ORR);
                    iLimORR = -1.0*mean(obj.filteredCurrentData(obj.currentRangesData_CathodicRxns(1,2*idxRxn2)));
                    param_vals = ElectrochemicalReductionReaction.LM_Fit_Activation2(obj.allCatRxnsModel(idxRxn), currVals-iLimORR, potVals); %currVals
                    z = 2;
                case reactionNames.ORR
                    N = length(currVals);
        
                    cE = currVals(N);
                    cS = currVals(1);
                    cLE = log(abs(cE));
                    cLS = log(abs(cS));
                    denom = cLE - cLS;
        
                    num = potVals(N) - potVals(1);
                    A = num/denom;
                    l1 = log(10);
                    kb = Constants.kb;
                    q = Constants.e;
                    T = obj.allCatRxnsModel(idxRxn).Temperature;
                    alpha = abs((l1*kb*T)/(A*q));    
                    if alpha > 1.0 || alpha < 0.0
                        alpha = 0.8;
                    end
                    obj.allCatRxnsModel(idxRxn).alpha = alpha;

                    param_vals = ElectrochemicalReductionReaction.LM_Fit_Activation2(obj.allCatRxnsModel(idxRxn), currVals, potVals); %currVals
                    z = 4;
                case reactionNames.Fe_Red
                    N = length(currVals);

                    idxRxn2 = obj.catReactsDict(reactionNames.ORR);
                    iactO = obj.allCatRxnsModel(idxRxn2).i(obj.currentRangesData_CathodicRxns(:,nR));

                    idxRxn3 = obj.catReactsDict(reactionNames.HER);
                    if obj.allCatRxnsModel(idxRxn3).i(obj.currentRangesData_CathodicRxns(1,nR)) > obj.outOfRangeVal
                        iactH = obj.allCatRxnsModel(idxRxn3).i(obj.currentRangesData_CathodicRxns(:,nR));
                    else
                        iactH(1:length(obj.currentRangesData_CathodicRxns(:,nR)),1) = 0.0;
                    end

                    iactT = iactO + iactH;

                    idxSmaller = 1;
                    idxGreater = 1;
                    for kk = 1:N
                        if abs(currVals(kk)) > abs(iactT(kk)) 
                            idxGreater = kk;
                        elseif abs(currVals(kk)) <= abs(iactT(kk))
                            idxSmaller = kk;
                        end
                    end
                    if idxSmaller < idxGreater
                        aRange = idxSmaller:1:idxGreater;
                    elseif idxSmaller > idxGreater
                        aRange = idxGreater:1:idxSmaller;                                   
                    end   
                    currVals4 = currVals - iactT; 
                    param_vals = ElectrochemicalReductionReaction.LM_Fit_Activation2(obj.allCatRxnsModel(idxRxn), currVals4(aRange), potVals(aRange));                    
                    z = 1; 
                otherwise
            end
           
            dG_cathodic = param_vals(1);  
            alpha = param_vals(2);   
              
            fF = fFlag + 1;
        end
    
        function qRatio = getQ(obj,idxRxn,N,vSite1,vSite2,vSite3)
            qRatio = ones(size(obj.filteredCurrentData));

            switch obj.allCatRxnsModel(idxRxn).name
            
                case reactionNames.ORR
                    [~,indexOfMinCurrent] = min(abs(obj.filteredCurrentData));
                    needGaussCorrection = true;
                    minV = abs(obj.potentialData(indexOfMinCurrent));
                    
                    if vSite3 >= minV
                        needGaussCorrection = false;
                    end
                    
%                     if obj.currentRangesData_CathodicRxns(1,6) > -1 && obj.currentRangesData_CathodicRxns(1,8) > -1
%                         needGaussCorrection = false;
%                     end
                    if needGaussCorrection == true
                        for j = N:-1:1
                            if (obj.potentialData(j) >= vSite3)
                                idXStartGauss = j;
                                break;
                            end
                        end      
                        iS = idXStartGauss; %obj.indicesForFitRegions(6);
                        iE = indexOfMinCurrent;
                        iP = obj.filteredCurrentData(iS); %filteredCurrentDataAdjusted
                        iP2 = iP/2.0;
                        idxP_R = iS; % 

                        vPeak = vSite3; %obj.potentialData(idxP_R);                           
                        for j = idxP_R:-1:iE
                            if obj.filteredCurrentData(j) <= iP2 %filteredCurrentDataAdjusted
                                idxHalf = j;
                                break;
                            end
                        end
                        vHalf = obj.potentialData(idxHalf);
                        
                        sigma1 = abs(vPeak - vHalf)/1.177;
                        sigma6 = 6.0 * sigma1;                            
                        endV = vPeak + sigma6;

                        idxEnd = -1;
                        for j = idxP_R:-1:iE
                            if abs(obj.potentialData(j)) <= abs(endV)
                                idxEnd = j;
                                break;
                            else 
                                idxEnd = iE;
                            end
                        end
%                         if idxEnd == -1
%                             [iM,idxEnd] = min(obj.filteredCurrentData(iS:-1:iE));
%                             idxEnd = idxEnd + iE;
%                         end

                        q0 = sqrt(2 * pi) * sigma1 * (iP);
                        dx = abs(vPeak - endV); % obj.potentialData(idxEnd)
                        aRHS = erf(dx /(sqrt(2) * sigma1)); 
                        qT = (q0 * aRHS); % Only half the area under the full Gaussian!

                        for j = 1:length(qRatio)
                            if j < idxEnd
                                qRatio(j) = 0.0;
                            elseif j >= idxEnd && j <= idxP_R
                                v = obj.potentialData(j);
                                dx = abs(vPeak - v);
                                lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                                qVal = (q0/2.0) * lVal;
                                qRatio(j) = qVal/qT;
                            elseif j > idxP_R && j < idxP_R + 2000
                                v = obj.potentialData(j);
                                dx = abs(vPeak - v);
                                lVal = erf(dx/(sqrt(2) * sigma1)); %1 - 
                                qVal = (q0/2.0) * (1.0 + lVal);   
                                qRatio(j) = qVal/qT;
                            end                                    
                        end                     
                    end
                    
                case reactionNames.Fe_Red
                    [~,indexOfMinCurrent] = min(abs(obj.filteredCurrentData));  
                    NeedHighPotGauss = true; %false; %
                    idXStartGauss = -1;
                    for j = indexOfMinCurrent:N
                        diff = abs(abs(obj.potentialData(j)) - abs(vSite1));
                        if diff <= 1.0e-2
                            idXStartGauss = j;
                            break;
                        end
                    end                
                    if idXStartGauss == -1
                        NeedHighPotGauss = false;
                        idXStartGauss = indexOfMinCurrent;
                    end
                    if NeedHighPotGauss == true
                        iS = indexOfMinCurrent; %idXStartGauss
                        iE = idXStartGauss; %N;
                        iP = obj.filteredCurrentData(iE); %obj.filteredCurrentData(iS); %Adjusted
                        idxP_L = iS;
                        vPeak = vSite1;
                        iP2 = iP/2.0;
                        
                        for j = idxP_L:iE
                            if obj.filteredCurrentData(j) >= iP2 %>= (iP + iP2) %Adjusted
                                idxHalf = j;
                                break;
                            end
                        end
                        vHalf = obj.potentialData(idxHalf);
                        
                        sigma1 = abs((abs(vPeak) - abs(vHalf))/1.177);
                        sigma6 = 6.0 * sigma1;                            
                        endV = vPeak - sigma6;
                        if endV <= obj.potentialData(indexOfMinCurrent)
                            endV = obj.potentialData(indexOfMinCurrent);
                        end

                        idxEnd = -1;
                        for j = idxP_L:iE
                            if abs(obj.potentialData(j)) >= abs(endV)
                                idxEnd = j;
                                break;
                            end
                        end
                        if idxEnd == -1
                            [iM,idxEnd] = min(obj.filteredCurrentDataAdjusted(idxP_L:iE));
                            idxEnd = idxEnd + idxP_L;
                        end

                        q0 = sqrt(2 * pi) * sigma1 * (iP);
                        dx = abs(vPeak - endV);
                        aLHS = erf(dx /(sqrt(2) * sigma1)); 
                        qT = (q0 * aLHS); % Only half the area under the full Gaussian!

                        for j = 1:length(qRatio)
                            if j < indexOfMinCurrent
                                qRatio(j) = 0.0;
                            elseif j >= indexOfMinCurrent && j <= idxEnd
                                v = obj.potentialData(j);
                                dx = abs(vPeak - v);
                                lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                                qVal = (q0/2.0) * lVal;
                                qRatio(j) = qVal/qT;                                            
                            elseif j > idxEnd && j <= idxP_L
                                v = obj.potentialData(j);
                                dx = abs(vPeak - v);
                                lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                                qVal = (q0/2.0) * lVal;
                                qRatio(j) = qVal/qT; 
                            elseif j > idxP_L && j <= iE
                                v = obj.potentialData(j);
                                dx = abs(vPeak - v);
                                lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                                qVal = (q0/2.0) * lVal;
                                qRatio(j) = qVal/qT; 
                            elseif j > iE
                                v = obj.potentialData(j);
                                dx = abs(vPeak - v);
                                lVal = erf(dx/(sqrt(2) * sigma1)); %1 - 
                                qVal = (q0/2.0) * (1.0 + lVal); 
                                qRatio(j) = qVal/qT;                                            
                            end                                        
                            if abs(qRatio(j) - 0.99) <= 1.0e-2 || abs(qRatio(j) - 1.01) <= 1.0e-2
                                pointsToExtend = j;
                                break;
                            else
                                pointsToExtend = length(qRatio);
                            end
                        end                                  
                    else
                        for j = 1:length(qRatio)
                            qRatio(j) = 1.0;
                        end
                    end
                    % =============================   
                    % Assume low cathodic potentials don't allow
                    % ORR to occur at all activation sites
                    % =============================
                    NeedLowPotGauss = true; %false; %
                    if NeedHighPotGauss == false
                        pointsToExtend = indexOfMinCurrent;
                    end
                    for j = 1:N
                        diff = abs(abs(obj.potentialData(j)) - abs(vSite2));
                        if diff <= 1.0e-4
                            idXStartGauss = j;
                            break;
                        end
                    end      
                    iS = idXStartGauss; %obj.indicesForFitRegions(6);
                    iE = N;
                    iP = obj.filteredCurrentData(iS); %filteredCurrentDataAdjusted
                    iP2 = iP/2.0;
                    idxP_R = iS; % 
                    
                    if NeedLowPotGauss == true
                        vPeak = vSite2; %obj.potentialData(idxP_R);                           
                        for j = idxP_R:iE
                            if obj.filteredCurrentData(j) >= (iP + iP2) %filteredCurrentDataAdjusted
                                idxHalf = j;
                                break;
                            end
                        end
                        vHalf = obj.potentialData(idxHalf);
                        
                        sigma1 = abs(vPeak - vHalf)/1.177;
                        sigma6 = 6.0 * sigma1;                            
                        endV = vPeak + sigma6;

                        idxEnd = -1;
                        for j = idxP_R:-1:iE
                            if abs(obj.potentialData(j)) <= abs(endV)
                                idxEnd = j;
                                break;
                            end
                        end
                        if idxEnd == -1
                            [iM,idxEnd] = min(obj.filteredCurrentData(iS:iE));
                            idxEnd = idxEnd + iE;
                        end

                        q0 = sqrt(2 * pi) * sigma1 * (iP);
                        dx = abs(vPeak - endV); % obj.potentialData(idxEnd)
                        aRHS = erf(dx /(sqrt(2) * sigma1)); 
                        qT = (q0 * aRHS); % Only half the area under the full Gaussian!

                        for j = 1:length(qRatio)
                            if j >= pointsToExtend && j <= idxP_R %idxP_L+
                                v = obj.potentialData(j);
                                dx = abs(vPeak - v);
                                lVal = erf(dx/(sqrt(2) * sigma1)); %1 - 
                                qVal = (q0/2.0) * (1.0 + lVal);   
                                qRatio(j) = qVal/qT;
                            elseif j > idxP_R %&& j < idxP_R + 2000
                                v = obj.potentialData(j);
                                dx = abs(vPeak - v);
                                lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
                                qVal = (q0/2.0) * lVal;
                                qRatio(j) = qVal/qT;
                            end    
                            
                        end                                  
                    end                    
            end
        end
    end
    % These are methods for plotting and file outputs
    methods (Static)
        
        function Plot_Polarization_Data_and_Model(obj,fig_num,aD)
            sName = split(obj.Name,'.');
            Plot_name = strcat(sName(1),'_',num2str(fig_num));
            % =============
            % figType   = 1     Replicate data + averaged result            
            % figType   = 2     Replicate data + averaged result + fit regions + individual reactions + model current
            % figType   = 3     Averaged result + fit regions
            % figType   = 4     Replicate data + averaged result + model current
            % =============
            figType = 2;

            h1 = figure(fig_num);

%             set(h1,'Position', [10 10 1200 1200])
            hold on

            % Plot the average polarization data as a thin black dashed
            % line
            plot(obj.currentData(1:length(obj.potentialData)),obj.potentialData,'--k','LineWidth', obj.plot_line_width-1)

            switch figType
                case 1
                    % Plot the individual measurements curves as thin dotted black lines
                    numGoodCurves = length(aD.goodCurve);
                    for i = 1:numGoodCurves
                        plot(aD.fiData(i,:),aD.pData(i,:),':k','LineWidth', obj.plot_line_width-2)
                    end    
                    legendString = {'Average Curve','Replicate Data'};
                case 2
                    % Plot the cathodic reaction currents
                    for i = 1:(length(obj.allCatRxnsModel)-1) % This accounts for the 'None' reaction
                        current = obj.allCatRxnsModel(i).i; 
                        potential = obj.potentialData; 
                        if current(1) > -1
                            plot(abs(current), potential, obj.allCatRxnsModel(i).plotSymbol,'LineWidth', obj.plot_line_width)
                        else
                            plot(1e-10,0.0,obj.allCatRxnsModel(i).plotSymbol,'LineWidth', obj.plot_line_width)
                        end                
                    end                
                    %Plot the anodic reaction currents
                    for i = 1:1 %length(obj.allAnRxnsModel)
                        current = abs(obj.allAnRxnsModel(i).i);
                        potential = obj.potentialData; %obj.anRxnsModel(i).eApp;                
                        plot(current, potential, obj.allAnRxnsModel(i).plotSymbol,'LineWidth', obj.plot_line_width)
                    end  
                    % Plot the model polarization curve as a thick black line
                    plot(abs(obj.iTotModel), obj.potentialData, '-k','LineWidth', obj.plot_line_width)                      
                    % Plot the individual measurements curves as thin dotted black lines
                    numGoodCurves = length(aD.goodCurve);
                    for i = 1:numGoodCurves
                        plot(aD.fiData(i,:),aD.pData(i,:),':k','LineWidth', obj.plot_line_width-2)
                    end                                            
                    % Plot the cathodic fit regions
                    for i = 1:5
                        indices = obj.currentRangesData_CathodicRxns(:,i);
                        if (indices(1,1) > -1)
                            plot(obj.filteredCurrentData(indices),obj.potentialRangesData_CathodicRxns(:,i),'-r','LineWidth', obj.plot_line_width+2)
                        else
                            continue;
                        end
                    end
                    % Plot the anodic fit region
                    for i = 1:1
                        indices = nonzeros(obj.currentRangesData_AnodicRxns(:,i));
                        plot(obj.filteredCurrentData(indices),obj.potentialRangesData_AnodicRxns(indices,i),'-r','LineWidth', obj.plot_line_width+2)
                    end   
%                     legendString = {'Average Curve','Reduction Reaction 1','Reduction Reaction 2','Reduction Reaction 3','Oxidation Reaction 1','Model Curve', 'Replicate Data'};
                    legendString = {'Average Data Curve','Fe reduction','ORR','HER','Oxidation','Model Curve', 'Replicate Data'};
                case 3
                    % Plot the cathodic fit regions
                    for i = 1:5
                        indices = obj.currentRangesData_CathodicRxns(:,i);
                        if (indices(1,1) > -1)
                            plot(obj.filteredCurrentData(indices),obj.potentialRangesData_CathodicRxns(:,i),'-r','LineWidth', obj.plot_line_width+2)
                        else
                            continue;
                        end
                    end
                    % Plot the anodic fit region
                    for i = 1:1
                        indices = nonzeros(obj.currentRangesData_AnodicRxns(:,i));
                        plot(obj.filteredCurrentData(indices),obj.potentialRangesData_AnodicRxns(indices,i),'-r','LineWidth', obj.plot_line_width+2)
                    end   
                    legendString = {'Average Curve','Fit Regions'};                
                case 4
                    % Plot the model polarization curve as a thick black line
                    plot(abs(obj.iTotModel), obj.potentialData, '-k','LineWidth', obj.plot_line_width)
                    % Plot the individual measurements curves as thin dotted black lines
                    numGoodCurves = length(aD.goodCurve);
                    for i = 1:numGoodCurves
                        plot(aD.fiData(i,:),aD.pData(i,:),':k','LineWidth', obj.plot_line_width-2)
                    end    
                    legendString = {'Average Curve','Model Curve','Replicate Data'};                    
                otherwise
            end

            axis square
            box on
            ylim([-1.3,0.0])
            xlim([1.0e-11,0.001])
            xlabel('Current density (A/cm^2)', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
            ylabel('Potential (V_{SCE})', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
            
            ax = gca;
            ax.XScale = 'log';
            ax.FontSize = obj.tick_label_size;
            ax.FontWeight = obj.font_weight;
            ax.LineWidth = obj.axis_line_width;
            ax.XTick = [1.0e-11,1.0e-10,1.0e-9,1.0e-8,1.0e-7,1.0e-6,1.0e-5,1.0e-4,1.0e-3];     
            ax.YTick = -1.3:0.2:0.1;   
            ax.XMinorTick = 'on';
            ax.YMinorTick = 'on';

            legend boxoff
            
            legend(legendString,'Location','best')
            exportgraphics(ax,strcat(char(Plot_name),'.png'),'Resolution',300)
            hold off            
 
        end        
        
        function OutputFitValues(fileName,TC,cCl,fVals)
%             fileName = 'AllPolarizationData.xlsx';
            if isfile(fileName)
                delete(char(fileName));
            end   
            writecell({'T','Cl-','dG_Cathodic','dG_Anodic','alpha','Diffusion_Length'},char(fileName),'Range','A1:F1')
            writematrix([TC,cCl],char(fileName),'Range','A2:B2')
            writematrix(fVals,char(fileName),'Range','C2:F5')
        end
        
        function Plot_Polarization_Data_and_Model_OtherFiles(obj,fig_num,fn1,fn2,fn3)

            data1 = readtable(fn1);
            data2 = readtable(fn2);
            data3 = readtable(fn3);

            sName = split(obj.Name,'.');
            Plot_name = strcat(sName(1),'_',num2str(fig_num));
            
            h1 = figure(fig_num);

%             set(h1,'Position', [10 10 1200 1200])
            hold on

            % Plot the polarization data as a thin black line
            plot(obj.filteredCurrentData,obj.potentialData,'--k','LineWidth', obj.plot_line_width-1)    

            % Plot the model polarization curve as a thick black line
            plot(abs(obj.iTotModel), obj.potentialData, '-k','LineWidth', obj.plot_line_width) %obj.eAppModel 
            
            plot(abs(data1.Im)./0.495,data1.Vf,':r','LineWidth', obj.plot_line_width-1)   
            plot(abs(data2.Im)./0.495,data2.Vf,'--r','LineWidth', obj.plot_line_width-1)  
            plot(abs(data3.Im)./0.495,data3.Vf,'-.r','LineWidth', obj.plot_line_width-1)  

            legendString = {'Data Curve','Model Curve','Cr Data','Fe Data','FeCr Data'};

            axis square
            box on
            ylim([-1.3,0.0])
            xlim([1.0e-11,0.001])
            xlabel('Current density (A/cm^2)', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
            ylabel('Potential (V_{SCE})', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
            
            ax = gca;
            ax.XScale = 'log';
            ax.FontSize = obj.tick_label_size;
            ax.FontWeight = obj.font_weight;
            ax.LineWidth = obj.axis_line_width;
%             ax.XTick = [1.0e-13,1.0e-12,1.0e-11,1.0e-10,1.0e-9,1.0e-8,1.0e-7,1.0e-6,1.0e-5,1.0e-4,1.0e-3,0.01,0.1];
            ax.XTick = [1.0e-11,1.0e-10,1.0e-9,1.0e-8,1.0e-7,1.0e-6,1.0e-5,1.0e-4,1.0e-3];
%             ax.YTick = -1.4:0.2:0.1;       
            ax.YTick = -1.3:0.2:0.1;   
            ax.XMinorTick = 'on';
            ax.YMinorTick = 'on';

            legend boxoff
            
            legend(legendString,'Location','best')
            exportgraphics(ax,strcat(char(Plot_name),'.png'),'Resolution',300)
            hold off 
        end        
               
    end

    % These are methods for loading and analyzing polarization curves
    methods (Static)

        function [fiData, iData, pData, rpData, goodCurve] = LoadAPolarizationCurve(fN, rSolution, acTruth, area)
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
            % Author:   Steve Policastro, Ph.D., Materials Science
            % Center for Corrosion Science and Engineering, U.S. Naval Research
            % Laboratory
            % email address: steven.policastro@nrl.navy.mil  
            % Website: 
            % October 2021; Last revision: 14-Oct-2021
            %==========================================================================
            
            %------------- BEGIN CODE --------------
            cutoffPoints = 256; %16; %32; %64; %128; %512; %1024; %
            extension = '.csv';
            headerLinesIn = 1;
            delimiterIn = ',';
    
            % Open and load the polarization curve file
            expData = importdata(fN,delimiterIn,headerLinesIn); 
            rawCurrent = str2double(expData.textdata(2:end,4));
            rawPotential = str2double(expData.textdata(2:end,3));

            % Perform the area correction to the current to get current
            % density
            if acTruth == true
                iAreaCorrected = rawCurrent./area;     
            else
                iAreaCorrected = rawCurrent;
            end
            absRawCurrent = abs(iAreaCorrected);            
            tempiData = absRawCurrent;   
            [~,minVal] = min(tempiData);

            % Perform the iR correction to the potential
            correctedPotential = zeros(size(rawPotential));
            for i = 1:length(correctedPotential)
                if rawPotential(i) <= 0.0
                    if i >= minVal
                        correctedPotential(i) =  rawPotential(i) + (absRawCurrent(i)*rSolution);
                    elseif i < minVal
                        correctedPotential(i) =  rawPotential(i) - (absRawCurrent(i)*rSolution);
                    end       
                elseif rawPotential(i) > 0.0
                    if i >= minVal
                        correctedPotential(i) =  rawPotential(i) - (absRawCurrent(i)*rSolution);
                    elseif i < minVal
                        correctedPotential(i) =  rawPotential(i) + (absRawCurrent(i)*rSolution);
                    end                    
                end                
            end
            
%             figure(9)
%             hold on
%             plot(absRawCurrent, correctedPotential, '-b')
%             plot(absRawCurrent, rawPotential, '-r')
%             ax = gca;
%             ax.XScale = 'log';
%             hold off
            
            cutOffCurrent = 1.0e-6;
            cutOffPotential = 0.01;
            endFile = length(correctedPotential);
            for i = 1:length(correctedPotential)
                if abs(absRawCurrent(i)) >= cutOffCurrent
                    if abs(abs(rawPotential(i)) - abs(correctedPotential(i))) >= cutOffPotential
                        endFile = i;
                        break;
                    end
                end
            end 

            goodCurve = true;
            for i = 100:length(correctedPotential)
                prevPot = correctedPotential(i-99);
                newPot = correctedPotential(i);

                if newPot <= -0.7
                    if (newPot > prevPot)
                        goodCurve = false;
                    end
                end                
            end
            
            % Filter the raw current data to remove noise            
            tempfiData = smoothdata(absRawCurrent,'sgolay',cutoffPoints);
            temppData = correctedPotential; %rawPotential;  
            
%             figure(10)
%             hold on
%             plot(absRawCurrent, correctedPotential, '-k')
%             plot(absRawCurrent, rawPotential, '-r')
%             plot(abs(tempfiData(1:endFile)), correctedPotential(1:endFile), '-b')           
%             ax = gca;
%             ax.XScale = 'log';
%             hold off

            iData = tempiData;
            fiData = tempfiData(1:endFile);
            pData = temppData(1:endFile);
            rpData = rawPotential;

%             tick_label_size = 16;
%             axis_label_size = 18;
%             title_label_size = 20;
%             axis_line_width = 3;
%             font_weight = 'bold';
%             plot_line_width = 3;
%             plot_line_width_2 = 2;
% 
%             figure(700)
%             hold on
%             plot(iData,rawPotential, '-r','LineWidth',3)
%             plot(fiData,pData(1:length(fiData)), '-b','LineWidth',3)
%             xlabel('Current Density (A/cm^{2})', 'FontSize', axis_label_size,'FontWeight',font_weight)
%             ylabel('Potential (V_{SCE})', 'FontSize', axis_label_size,'FontWeight',font_weight)
%             legend('Raw data', 'iR Corrected and Noise Filtered', 'Location', 'southwest')
%             ax = gca;          
%             ax.XScale = 'log';
%             ax.FontSize = tick_label_size;
%             ax.FontWeight = font_weight;
%             ax.LineWidth = axis_line_width;
%             axis square
%             box on
%             legend boxoff
%             hold off

            %------------- END OF CODE --------------
        end

        function [currentRangesData_CR, potentialRangesData_CR, currentRangesData_AR, potentialRangesData_AR, polarizationCurveAnalysisPoints] = AnalyzePolarizationCurve(obj)
            %AnalyzePolarizationCurve - Analyze polariation data
            %
            %The purpose of this function is to identify regions in the
            %potential and current data that can form the observational
            %data for the regression algorithms.  It requires the following
            %inputs:
            % =============================================
            % obj                                   = an object of type CathodicPotentiodynamicExperiment
            % =============================================
            % The function returns the following:
            % =============================================
            % currentRangesData_CR                  = Multidimensional array of indices for where cathodic reaction current data should be extracted for fitting
            % potentialRangesData_CR                = Multidimenstional array of potential data for the fit routines
            % currentRangesData_AR                  = Multidimensional array of indices for where anodic reaction current data should be extracted for fitting
            % potentialRangesData_AR                = Multidimenstional array of potential data for the fit routines
            % polarizationCurveAnalysisPoints       = Array of indices which are the center points for extracting the current data for the fitting routines
            % =============================================           
            nTotalDatatPoints = length(obj.currentData);
            numCurrentRangesReduction = obj.numCatReactions*2; % 8;
            numCurentRangesOxidation = obj.numAnReactions; %3;

            if nTotalDatatPoints < 1000
                nPointsPerRange = 50;                
            elseif nTotalDatatPoints >= 1000 && nTotalDatatPoints < 10000
                nPointsPerRange = 200;               
            else
                nPointsPerRange = 500;         
            end
            midPoint = round(nPointsPerRange/2);

            currentRangesData_CR = zeros(nPointsPerRange,numCurrentRangesReduction);
            potentialRangesData_CR = zeros(nPointsPerRange,numCurrentRangesReduction);
            
            currentRangesData_AR = zeros(nPointsPerRange,numCurentRangesOxidation);
            potentialRangesData_AR = zeros(nPointsPerRange,numCurentRangesOxidation);

            switch length(obj.inclCatRxnsModel)
                case 1
                    numberOFAnalysisPoints = 4;
                case 2
                    numberOFAnalysisPoints = 8;
                case 3
                    % Allocation of analysis points
                    %[  1 = Left end of ilim ORR Peak
                    %   2 = Middle of ilim ORR Peak
                    %   3 = Right end of ilim ORR Peak
                    %   4 = iact ORR
                    %   5 = Left end of Rxn 1 Peak
                    %   6 = Middle of Rxn 1 Peak
                    %   7 = Right end of Rxn 1 Peak
                    %   8 = iact Rxn1
                    %   9 = iact HER
                    %   10 = iact anodic rxn]                    
                    numberOFAnalysisPoints = 10;
                otherwise
                    numberOFAnalysisPoints = 4;
            end

            polarizationCurveAnalysisPoints = zeros(1,numberOFAnalysisPoints);
            [~,iM] = min(abs(obj.filteredCurrentData));
            indexOFLastValue = length(obj.filteredCurrentData);
            basePoints = 1:1:nTotalDatatPoints;
            offset1Cathodic = iM:1:indexOFLastValue;
            offset1Anodic = 1:1:iM-1;
            % ==================================
            % Find the region to fit on the anodic curve
            % ==================================
            nAnodicPoints = offset1Anodic(end);
            idXAnodicStart = 1;
            if (nAnodicPoints > nPointsPerRange)
                currentRangesData_AR(1:nPointsPerRange,1) = 1:nPointsPerRange; %increasePercent.*obj.currentData(1:nPointsPerRange);
                potentialRangesData_AR(1:nPointsPerRange,1) = obj.potentialData(1:nPointsPerRange);                 
                idXAnodicEnd = nPointsPerRange;
            else
                currentRangesData_AR(1:nAnodicPoints,1) = offset1Anodic; %increasePercent.*obj.currentData(1:nAnodicPoints);
                potentialRangesData_AR(1:nAnodicPoints,1) = obj.potentialData(offset1Anodic); %1:nAnodicPoints
                idXAnodicEnd = nAnodicPoints;
            end
            
            % ==================================            
            % ==================================
            % First Derivatives of current density data
            % ==================================
            iRange = obj.filteredCurrentData(offset1Cathodic);
            pRange = obj.potentialData(offset1Cathodic);
            D1C = abs(CathodicPotentiodynamicExperiment.ObtainFilteredFirstDerivative(iRange,pRange));

            iRange = obj.filteredCurrentData(offset1Anodic);
            pRange = obj.potentialData(offset1Anodic);
            D1A = abs(CathodicPotentiodynamicExperiment.ObtainFilteredFirstDerivative(iRange,pRange));           
            % ==================================
            % Second Derivatives of current density values
            % ==================================
            iRange = obj.filteredCurrentData(offset1Cathodic); %D1C; %
            pRange = obj.potentialData(offset1Cathodic);            
            D2C = CathodicPotentiodynamicExperiment.ObtainFilteredSecondDerivative(iRange, pRange, D1C); %CathodicPotentiodynamicExperiment.ObtainFilteredFirstDerivative(iRange,pRange); %

            iRange = obj.filteredCurrentData(offset1Anodic);
            pRange = obj.potentialData(offset1Anodic);
            D2A = CathodicPotentiodynamicExperiment.ObtainFilteredSecondDerivative(iRange, pRange, D1A);     
            % ==================================

            for i = 1:length(D2C)
                if abs(D2C(i)) < 0.5
                    indexOfMinCurrent = i;
                    break;
                end
            end
            offset2Cathodic = (indexOfMinCurrent+iM):1:indexOFLastValue;

            sectionOfD1C = abs(D1C(indexOfMinCurrent:length(D1C)));
            max1D = max(sectionOfD1C);
            scaledSectionD1C = sectionOfD1C./max1D;   

            sectionOfD2C = D2C(indexOfMinCurrent:length(D2C));
            max2D = abs(max(sectionOfD2C));
            scaledSectionD2C = sectionOfD2C./max2D;  
            
            obj.polCurveFirstDerivativeC = zeros(size(obj.filteredCurrentData));
            obj.polCurveSecondDerivativeC = zeros(size(obj.filteredCurrentData));

            s1 = offset1Anodic(1);
            e1 = offset1Anodic(end);
            s2 = offset1Cathodic(1);
            e2 = offset2Cathodic(1) - 1;
            s3 = offset2Cathodic(1);
            e3 = offset2Cathodic(end);
            obj.polCurveFirstDerivativeC(s1:e1) = -1.0;
            obj.polCurveFirstDerivativeC(s2:e2) = -1.0;            
            obj.polCurveFirstDerivativeC(s3-1:e3) = scaledSectionD1C;

            obj.polCurveSecondDerivativeC(s1:e1) = -2.0;
            obj.polCurveSecondDerivativeC(s2:e2) = -2.0;            
            obj.polCurveSecondDerivativeC(s3-1:e3) = scaledSectionD2C;

            
            obj.polCurveFirstDerivativeA = D1A;
            obj.polCurveSecondDerivativeA = D2A;

            plot_derivs = true; % false; % 
            if plot_derivs
                Plot_Polarization_Data_Analysis(obj,obj.filteredCurrentData,200)
            end
            
            polarizationCurveAnalysisPoints(1,10) = indexOfMinCurrent;


            % ==================================
            % Scan first derivative to locate points for cathodic reaction analyses
            % Find peak index for ilim - ORR
            % ====================================
            Peak1Exists = true;            
            [max1D, idxPeak1_Max] = max(scaledSectionD1C);
                        
            if abs(obj.filteredCurrentData(idxPeak1_Max)) <= 1.0e-5
                Peak1Exists = false;
                idxPeak1_Max = -1;
            end
            % Work on the left side of the peak
            if Peak1Exists == true
                halfMax = 0.5*max1D;
                for i = idxPeak1_Max:-1:1
                    val2D = abs(scaledSectionD2C(i)); 
                    val1D = scaledSectionD1C(i);
                    if val2D <= 0.002 && val1D < halfMax && i <= (idxPeak1_Max-1000)
                        idxEndPeak1_Left = i;
                        break;
                    end
                end
                % Work on the right side of the peak
                for i = idxPeak1_Max:1:length(scaledSectionD1C)
                    val2D = abs(scaledSectionD2C(i)); 
                    val1D = scaledSectionD1C(i);
                    if val2D <= 0.002 && val1D < halfMax && i >= (idxPeak1_Max+1000)
                        idxEndPeak1_Right = i;
                        break;
                    end                
                end    
            end

%             minValueForPeak2ToExist = 0.07 * max1D;
            % ====================================
            % Find peak for ilim - secondary reaction
            % ====================================
%             Peak2Exists = false;
            if Peak1Exists == true                
                
                [max2D,idxPeak2_Max] = max(scaledSectionD1C(1:idxEndPeak1_Left));

                if idxPeak2_Max >= idxEndPeak1_Left - 5
                    startSearch2ndPeak = 200;
                    for i = 200:idxEndPeak1_Left
                        val2D = scaledSectionD2C(i);
                        if val2D <= 0.0
                            startSearch2ndPeak = i;
                            break;
                        end
                    end
                    endSearch2ndPeak = -1;

                    for i = startSearch2ndPeak:idxEndPeak1_Left
                        val2D = scaledSectionD2C(i);
                        if val2D >= 0.0
                            endSearch2ndPeak = i;
                            break;
                        end
                    end
    
                    [max2D,idxPeak2_Max] = max(scaledSectionD1C(startSearch2ndPeak:endSearch2ndPeak));
                    idxPeak2_Max = idxPeak2_Max + startSearch2ndPeak - 1;
                end
                if idxPeak2_Max <= 5
                    Peak2Exists = false;
                else 
                    Peak2Exists = true;
                end
                
                if Peak2Exists == true
                    % Left side peak
                    valML = abs(scaledSectionD2C(idxPeak2_Max));
                    scanLeft = idxPeak2_Max-200;
                    if scanLeft < 1
                        scanLeft = 1;
                    end
                    for i = idxPeak2_Max:-1:scanLeft
                        val2D = abs(scaledSectionD2C(i));
                        if val2D >= valML
                            idxEndPeak2_Left = i;
                            valML = val2D;
                        end
                    end
                    
                    % Right side peak
                    valML = abs(scaledSectionD2C(idxPeak2_Max));
                    scanRight = idxPeak2_Max+200;
                    if scanRight >= idxEndPeak1_Left
                        scanRight = idxEndPeak1_Left;
                    end
                    for i = idxPeak2_Max:scanRight
                        val2D = abs(scaledSectionD2C(i));
                        if val2D >= valML
                            idxEndPeak2_Right = i;
                            valML = val2D;
                        end
                    end
                    if idxEndPeak2_Right == length(scaledSectionD1C)
                        idxEndPeak2_Right = length(scaledSectionD1C) - 2;
                    end
                end
%                 if Peak2Exists == false
%                     idxPeak2_Max = round((idxEndPeak1_Left)/2.0);
%                     max2D = scaledSectionD1C(idxPeak2_Max);
%                     Peak2Exists = true;
%                 end                
            else
                [max2D, idxMax2D2] = max(scaledSectionD1C);
                idxPeak2_Max  = idxMax2D2; 
                Peak2Exists = true;

                halfMax = 0.5*max2D;
                 % Work on the left side of the peak
                for i = idxPeak2_Max:-1:1
                    val2D = abs(scaledSectionD2C(i)); 
                    val1D = scaledSectionD1C(i);
                    if val2D <= 0.002 && val1D < halfMax
                        idxEndPeak2_Left = i;
                        break;
                    end
                end
                % Work on the right side of the peak
                idxEndPeak2_Right = -1;
                for i = idxPeak2_Max:1:length(sectionOfD1C)
                    val2D = abs(scaledSectionD2C(i)); 
                    val1D = scaledSectionD1C(i);
                    if val2D <= 0.002 && val1D < halfMax
                        idxEndPeak2_Right = i;
                        break;
                    end                
                end                    
                if idxEndPeak2_Right == -1
                    idxEndPeak2_Right = length(sectionOfD1C) - 2;
                end
            end
            
            % ====================================
            % ====================================
            % Identify points for the activation-controlled regions
            % ====================================
            % Activation region ORR
            if Peak1Exists == true && Peak2Exists == true
                aSection = scaledSectionD1C(idxEndPeak2_Right:idxEndPeak1_Left);
                [minVal,idx3] = min(aSection);
                idxActivation_iORR = idx3 + idxEndPeak2_Right - 1; %round((idxEndPeak1_Left + idxEndPeak2_Right)/2.0);
                if idxActivation_iORR == idxEndPeak2_Right
                    idxActivation_iORR = idxEndPeak2_Right + 500;
                end
                if idxActivation_iORR <= polarizationCurveAnalysisPoints(1,10)
                    avgVal = round((idxEndPeak1_Left - 1)/2.0);
                    idxActivation_iORR = avgVal + 1;
                end
            elseif Peak1Exists == true && Peak2Exists == false
                aSectionOfD1C = scaledSectionD1C(1:idxEndPeak1_Left);
                [minVal, idxActivation_iORR] = min(aSectionOfD1C);

                if idxActivation_iORR <= polarizationCurveAnalysisPoints(1,10)
                    avgVal = round((idxEndPeak1_Left - 1)/2.0);
                    idxActivation_iORR = avgVal + 1;
                end
%                 for i = idxEndPeak1_Left:-1:1
%                     if scaledSectionD1C(i) <= 1.0e-3*scaledSectionD1C(idxPeak1_Max)
%                         idxActivation_iORR = i;
%                         break;
%                     end
%                 end

            elseif Peak1Exists == false && Peak2Exists == true
                if idxEndPeak2_Right == length(scaledSectionD1C)
                    idxActivation_iORR = length(scaledSectionD1C) - 5; %round((idxEndPeak2_Right + length(scaledSectionD1C))/2.0);
                else
                    hPoint = round((length(scaledSectionD1C) - idxEndPeak2_Right)/2.0);
                    idxActivation_iORR = idxEndPeak2_Right + hPoint;
                end
                
            end

            % Activation region 2nd Rxn
            if Peak2Exists
%                 [max2D3, idxMax2D3] = max(obj.polCurveFirstDerivative(indexOfMinCurrent:idxEndPeak2_Left));
%                 idxActivation_Rxn2  = idxMax2D3 + indexOfMinCurrent;
%                 idxOfAPeak  = idxMax2D3 + indexOfMinCurrent;
%                 [min2D3, idxMin2D3] = min(obj.polCurveSecondDerivative(idxOfAPeak:idxEndPeak2_Left));
%                 idxActivation_Rxn2 = idxMin2D3 + idxOfAPeak;
                idxActivation_Rxn2 = round((idxEndPeak2_Left - 1)/2.0);   % + indexOfMinCurrent
            else
                idxActivation_Rxn2 = -1;
            end

            % Activation region HER
            idxActivation_HER = (length(scaledSectionD1C)-1) - (2*midPoint) - 1;

%             iCheck = 5.0 * obj.filteredCurrentData(idxMaxPeak1);
%             for i = idxEndPeak1_Right:idxStep:indexOFLastValue
%                 if obj.filteredCurrentData(i) >= iCheck
%                     idxActivation_HER = i;
%                     break;
%                 end
%             end
            % ====================================
%             idxActivation_HER = length(obj.filteredCurrentData) - (2*midPoint) - 1;
%             countOnes = 0;
%             countZeros = 0;
%             for i = 1:length(deleteThese)
%                 if deleteThese(i) == 1
%                     countOnes = countOnes + 1;
%                 end
%                 if deleteThese(i) == 0
%                     countZeros = countZeros+1;
%                 end
%                 if countZeros == idxPeak1_Max
%                     break;
%                 end
%             end
%             idxEndPeak1_Left = idxPeak1_Max + countOnes;
            if Peak1Exists == true
                idxActivation_HER = idxActivation_HER + (s3-1);
                if abs(obj.filteredCurrentData(idxActivation_HER)) < 1.0e-4
                    idxActivation_HER = -1;
                end                
            else
                idxActivation_HER = -1;
            end

            if Peak1Exists == true
                polarizationCurveAnalysisPoints(1,1) = idxEndPeak1_Left + (s3-1);
                polarizationCurveAnalysisPoints(1,2) = idxPeak1_Max + (s3-1);
                polarizationCurveAnalysisPoints(1,3) = idxEndPeak1_Right + (s3-1);                
            else
                polarizationCurveAnalysisPoints(1,1) = obj.outOfRangeVal;
                polarizationCurveAnalysisPoints(1,2) = obj.outOfRangeVal;
                polarizationCurveAnalysisPoints(1,3) = obj.outOfRangeVal;                
            end

            if Peak2Exists == true
                polarizationCurveAnalysisPoints(1,5) = idxEndPeak2_Left + (s3-1); %712 + (s3-1); %
                polarizationCurveAnalysisPoints(1,6) = idxPeak2_Max + (s3-1); %912 + (s3-1); %
                polarizationCurveAnalysisPoints(1,7) = idxEndPeak2_Right + (s3-1); %1112 + (s3-1); %1146 + (s3-1); %
            else
                polarizationCurveAnalysisPoints(1,5) = obj.outOfRangeVal;
                polarizationCurveAnalysisPoints(1,6) = obj.outOfRangeVal;
                polarizationCurveAnalysisPoints(1,7) = obj.outOfRangeVal;                
            end
            % ====================================
            polarizationCurveAnalysisPoints(1,4) = idxActivation_iORR + (s3-1); %1535 + (s3-1); %
            if Peak2Exists == true
                polarizationCurveAnalysisPoints(1,8) = idxActivation_Rxn2 + (s3-1); %424 + (s3-1); %
            else 
                polarizationCurveAnalysisPoints(1,8) = obj.outOfRangeVal;
            end
            if Peak1Exists == true && idxActivation_HER > obj.outOfRangeVal
                polarizationCurveAnalysisPoints(1,9) = idxActivation_HER + (s3-1);            
            end
            % ====================================
            % ====================================
            figure(600)
            hold on

            x = abs(obj.filteredCurrentData);
            y = obj.potentialData;
            yyaxis left 
            plot(x,y,'-k','LineWidth',3)

            yyaxis right
            xAll = obj.filteredCurrentData;
            yAll = obj.polCurveFirstDerivativeC;
            plot(xAll,yAll,'-r','LineWidth',2)

            if Peak1Exists == true
                x1 = obj.filteredCurrentData(idxEndPeak1_Left + (s3-1):idxEndPeak1_Right + (s3-1));
                y1 = obj.polCurveFirstDerivativeC(idxEndPeak1_Left + (s3-1):idxEndPeak1_Right + (s3-1));            
                plot(x1,y1,'-b','LineWidth',3)
            end

            x3 = obj.filteredCurrentData(idxActivation_iORR + (s3-1));
            y3 = obj.polCurveFirstDerivativeC(idxActivation_iORR + (s3-1));
            plot(x3,y3, 'bo', 'MarkerSize',8,'LineWidth',3)            

            if Peak2Exists == true
                s1 = idxEndPeak2_Left + (s3-1);
                e1 = idxEndPeak2_Right + (s3-1);
                x2 = obj.filteredCurrentData(s1:e1);
                y2 = obj.polCurveFirstDerivativeC(s1:e1);            
                plot(x2,y2,'-c','LineWidth',3)

                x4 = obj.filteredCurrentData(idxActivation_Rxn2 + (s3-1));
                y4 = obj.polCurveFirstDerivativeC(idxActivation_Rxn2 + (s3-1));
                plot(x4,y4, 'c^', 'MarkerSize',8,'LineWidth',3)
            end
            
            if idxActivation_HER > 0
                x5 = obj.filteredCurrentData(idxActivation_HER);
                y5 = obj.polCurveFirstDerivativeC(idxActivation_HER);
                plot(x5,y5, 'gs', 'MarkerSize',8,'LineWidth',3)  
            end

            x6 = obj.filteredCurrentData(idXAnodicStart);
            y6 = obj.polCurveFirstDerivativeA(idXAnodicStart);
            plot(x6,y6, 'ko', 'MarkerSize',8,'LineWidth',3) 

            x7 = obj.filteredCurrentData(idXAnodicEnd);
            y7 = obj.polCurveFirstDerivativeA(idXAnodicEnd);
            plot(x7,y7, 'ko', 'MarkerSize',8,'LineWidth',3) 
            
            xlim([1.0e-11,0.001])
            ax = gca;
            ax.XScale = 'log';
%             ax.YScale = 'log';            
            ax.FontSize = obj.tick_label_size;
            ax.FontWeight = obj.font_weight;
            ax.LineWidth = obj.axis_line_width;

            box on
            hold off  
            % ====================================
            % ====================================
            % Second Rxn activation
            if polarizationCurveAnalysisPoints(1,8) > 0
                currentRangesData_CR(1:nPointsPerRange,1) = polarizationCurveAnalysisPoints(1,8)-midPoint+1:polarizationCurveAnalysisPoints(1,8)+midPoint; %obj.filteredCurrentData(indexForiORRAcidAct-midPoint+1:indexForiORRAcidAct+midPoint);
                potentialRangesData_CR(1:nPointsPerRange,1) = obj.potentialData(polarizationCurveAnalysisPoints(1,8)-midPoint+1:polarizationCurveAnalysisPoints(1,8)+midPoint);
            else
                currentRangesData_CR(1:nPointsPerRange,1) = obj.outOfRangeVal;
                potentialRangesData_CR(1:nPointsPerRange,1) = obj.outOfRangeVal;  
            end

            % Second Rxn iLim
            if polarizationCurveAnalysisPoints(1,6) > 0
                currentRangesData_CR(1:nPointsPerRange,2) = polarizationCurveAnalysisPoints(1,6)-midPoint+1:polarizationCurveAnalysisPoints(1,6)+midPoint; %reductionPercent.*obj.filteredCurrentData(indexForiLimORRAcid-midPoint+1:indexForiLimORRAcid+midPoint);
                potentialRangesData_CR(1:nPointsPerRange,2) = obj.potentialData(polarizationCurveAnalysisPoints(1,6)-midPoint+1:polarizationCurveAnalysisPoints(1,6)+midPoint);
            else
                currentRangesData_CR(1:nPointsPerRange,2) = obj.outOfRangeVal;
                potentialRangesData_CR(1:nPointsPerRange,2) = obj.outOfRangeVal; 
            end

            % ORR alkaline activation
            if (polarizationCurveAnalysisPoints(1,4)-midPoint > 0)
                if polarizationCurveAnalysisPoints(1,4) < length(obj.potentialData)
                    e1 = polarizationCurveAnalysisPoints(1,4)+midPoint;
                    if e1 > length(obj.potentialData)
                        s1 = polarizationCurveAnalysisPoints(1,4)-(2*midPoint)+1;
                        e1 = polarizationCurveAnalysisPoints(1,4);               
                    else 
                        s1 = polarizationCurveAnalysisPoints(1,4)-midPoint+1;
                    end
                else
                    s1 = polarizationCurveAnalysisPoints(1,4)-(2*midPoint)+1;
                    e1 = polarizationCurveAnalysisPoints(1,4);
                end
                    currentRangesData_CR(1:nPointsPerRange,3) = s1:e1; %obj.filteredCurrentData(indexforiORRAlkAct-midPoint+1:indexforiORRAlkAct+midPoint);
                    potentialRangesData_CR(1:nPointsPerRange,3) = obj.potentialData(s1:e1);                 
            else
                currentRangesData_CR(1:nPointsPerRange,3) = polarizationCurveAnalysisPoints(1,4)+1:polarizationCurveAnalysisPoints(1,4)+nPointsPerRange; %obj.filteredCurrentData(indexforiORRAlkAct-midPoint+1:indexforiORRAlkAct+midPoint);
                potentialRangesData_CR(1:nPointsPerRange,3) = obj.potentialData(polarizationCurveAnalysisPoints(1,4)+1:polarizationCurveAnalysisPoints(1,4)+nPointsPerRange); 
            end

            % ORR alkaline iLim
            if polarizationCurveAnalysisPoints(1,2) > 0
                currentRangesData_CR(1:nPointsPerRange,4) = polarizationCurveAnalysisPoints(1,2)-midPoint+1:polarizationCurveAnalysisPoints(1,2)+midPoint; %reductionPercent.*obj.filteredCurrentData(indexForiLimORRAlk-midPoint+1:indexForiLimORRAlk+midPoint);
                potentialRangesData_CR(1:nPointsPerRange,4) = obj.potentialData(polarizationCurveAnalysisPoints(1,2)-midPoint+1:polarizationCurveAnalysisPoints(1,2)+midPoint);
            else
                currentRangesData_CR(1:nPointsPerRange,4) = obj.outOfRangeVal;
                potentialRangesData_CR(1:nPointsPerRange,4) = obj.outOfRangeVal;
            end

            % HER alkaline activation
            if polarizationCurveAnalysisPoints(1,9) > 0.0
                if polarizationCurveAnalysisPoints(1,3) < length(obj.filteredCurrentData)
                    if (polarizationCurveAnalysisPoints(1,9)+midPoint) <= indexOFLastValue
                        currentRangesData_CR(1:nPointsPerRange,5) = polarizationCurveAnalysisPoints(1,9)-midPoint+1:polarizationCurveAnalysisPoints(1,9)+midPoint; %obj.filteredCurrentData(indexForiHERAlxAct-midPoint+1:indexForiHERAlxAct+midPoint);
                        potentialRangesData_CR(1:nPointsPerRange,5) = obj.potentialData(polarizationCurveAnalysisPoints(1,9)-midPoint+1:polarizationCurveAnalysisPoints(1,9)+midPoint);
                    else
                        polarizationCurveAnalysisPoints(1,9) = indexOFLastValue - midPoint;
                        currentRangesData_CR(1:nPointsPerRange,5) = polarizationCurveAnalysisPoints(1,9)-midPoint+1:polarizationCurveAnalysisPoints(1,9)+midPoint; %obj.filteredCurrentData(indexForiHERAlxAct-midPoint+1:indexForiHERAlxAct+midPoint);
                        potentialRangesData_CR(1:nPointsPerRange,5) = obj.potentialData(polarizationCurveAnalysisPoints(1,9)-midPoint+1:polarizationCurveAnalysisPoints(1,9)+midPoint);   
                    end
                else
                    currentRangesData_CR(1:nPointsPerRange,5) = obj.outOfRangeVal; %obj.filteredCurrentData(indexOFLastValue); 
                    potentialRangesData_CR(1:nPointsPerRange,5) = obj.outOfRangeVal;   
                end                 
            else 
                currentRangesData_CR(1:nPointsPerRange,5) = obj.outOfRangeVal; %obj.filteredCurrentData(indexOFLastValue); 
                potentialRangesData_CR(1:nPointsPerRange,5) = obj.outOfRangeVal;                 
            end

            % HER alkaline iLim
            currentRangesData_CR(1:nPointsPerRange,6) = obj.outOfRangeVal; 
            potentialRangesData_CR(1:nPointsPerRange,6) = obj.outOfRangeVal;    
        end

        function dydx = ObtainFilteredFirstDerivative(fI, pD)
            polCurveDeriv = CathodicPotentiodynamicExperiment.FirstDerivative(fI, pD);            
            A = smoothdata(polCurveDeriv,'sgolay',128);
            k = (3*10) + 1; %nPointsPerRange
            M = movmean(A,k); 
            dydx = M; 
        end
    
        function dyydxx = ObtainFilteredSecondDerivative(fI, pD, dydx)
            polCurve2Deriv = CathodicPotentiodynamicExperiment.SecondDerivative(fI, pD, dydx);            
            A = smoothdata(polCurve2Deriv,'sgolay',150);
            k = (3*10) + 1; %nPointsPerRange
            M = movmean(A,k); 
            dyydxx = M; 
        end
    
        function yP = FirstDerivative(xExt, yExt)
            %https://www.dam.brown.edu/people/alcyew/handouts/numdiff.pdf
            totVals = length(yExt);
            xInt = log10(abs(xExt)); 
            yInt = yExt; 

            yP = zeros(size(yInt));
            idxCounter = 2;

            % First value - forward difference
            nTerm1 = -3*yInt(1);
            nTerm2 = 4*yInt(2);
            nTerm3 = -yInt(3);
            dTerm1 = xInt(3);
            dTerm2 = xInt(1);   
            firstTerm = (nTerm1 + nTerm2 + nTerm3)/(dTerm1 - dTerm2);
            yP(1) = firstTerm;
            
            % Middle values - centered difference            
            for i = 2:totVals-1            
                nTerm1 = yInt(i+1);
                nTerm2 = yInt(i-1);
                dTerm1 = xInt(i+1);
                dTerm2 = xInt(i-1);
                dTerm3 = dTerm1 - dTerm2;
                if abs(dTerm3) <= 1.0e-20
                    dTerm3 = 1.0e-20;
                end
                val = (nTerm1 -  nTerm2) / dTerm3;
                yP(idxCounter) = val;
                idxCounter = idxCounter + 1;
            end

            % Last value - backward difference     
            nTerm1 = 3*yInt(totVals);
            nTerm2 = -4*yInt(totVals-1);
            nTerm3 = yInt(totVals-2);
            dTerm1 = xInt(totVals);
            dTerm2 = xInt(totVals-2);
            lastTerm = (nTerm1 + nTerm2 + nTerm3)/(dTerm1 -dTerm2);
            yP(totVals) = lastTerm;            
        end
   
        function yPP = SecondDerivative(xExt, yExt, yP)
            %https://www.dam.brown.edu/people/alcyew/handouts/numdiff.pdf
            totVals = length(yExt);

            xInt = log10(abs(xExt));
            yInt = yExt;
            
            idxCounter = 2;
            yPP = zeros(size(yP));

            nterm1 = 2*yInt(1); %y(3,1);
            nterm2 = -5*yInt(2); %y(2,1);
            nterm3 = 4*yInt(3); %y(1,1);
            nterm4 = -yInt(4); %yP(1,1);
            hterm1 = xInt(4) - xInt(3);
            hterm2 = xInt(3) - xInt(2);
            hterm3 = xInt(2) - xInt(1);

            nterm = nterm1 + nterm2 + nterm3 + nterm4;
            dterm = (hterm1^2/3) + (hterm2^2/3) + (hterm3^2/3);
            val =  nterm/dterm;
            yPP(1) = val;

            for i = 2:totVals-1
                nterm1 = yInt(i + 1);                
                nterm2 = yInt(i);
                nterm3 = yInt(i - 1);
                nterm4 = yP(idxCounter);
                hterm1 = xInt(i+1) - xInt(i);
                hterm2 = xInt(i) - xInt(i-1);
        
                nterm = (nterm1 - 2*nterm2 + nterm3 - (hterm1 - hterm2) * nterm4);
                dterm = (hterm1^2/2 + hterm2^2/2);
                val =  nterm/dterm;
                yPP(idxCounter) = val;
                idxCounter = idxCounter + 1;             
            end

            nterm1 = 2*yInt(totVals); %y(iMax,1);
            nterm2 = -5*yInt(totVals-1); %y(iMax - 1,1);
            nterm3 = 4*yInt(totVals-2); %y(iMax - 2,1);
            nterm4 = -yInt(totVals-3); %yP(1,length(yP));
            hterm1 = xInt(totVals-2) - xInt(totVals-3); %adjX(iMax,1) - adjX(iMax - 1,1);
            hterm2 = xInt(totVals-1) - xInt(totVals-2); %adjX(iMax - 1,1) - adjX(iMax - 2,1);
            hterm3 = xInt(totVals) - xInt(totVals-1);

            nterm = nterm1 + nterm2 + nterm3 + nterm4;
            dterm = (hterm1^2/3) + (hterm2^2/3) + (hterm3^2/3);   
            val =  nterm/dterm;
            yPP(totVals) = val;

            T = isinf(yPP);
            for i = 1:length(T)
                if T(i) == 1
                    disp('Problem!')
                end
            end
        end
        
    end
end
%------------- END OF CODE --------------
