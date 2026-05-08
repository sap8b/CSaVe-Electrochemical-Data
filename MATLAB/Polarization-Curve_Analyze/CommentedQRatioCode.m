%             switch obj.allCatRxnsModel(idxRxn).name
%                 case reactionNames.HER
%                 
%                 case reactionNames.ORR
%                     [~,indexOfMinCurrent] = min(abs(obj.filteredCurrentData));
%                     needGaussCorrection = true;
%                     minV = abs(obj.potentialData(indexOfMinCurrent));
%                     if vSite3 >= minV
%                         needGaussCorrection = false;
%                     end
%                     
% %                     if obj.currentRangesData_CathodicRxns(1,6) > -1 && obj.currentRangesData_CathodicRxns(1,8) > -1
% %                         needGaussCorrection = false;
% %                     end
%                     if needGaussCorrection == true
%                         for j = N:-1:1
%                             if (obj.potentialData(j) >= vSite3)
%                                 idXStartGauss = j;
%                                 break;
%                             end
%                         end      
%                         iS = idXStartGauss; %obj.indicesForFitRegions(6);
%                         iE = indexOfMinCurrent;
%                         iP = obj.filteredCurrentData(iS); %filteredCurrentDataAdjusted
%                         iP2 = iP/2.0;
%                         idxP_R = iS; % 
% 
%                         vPeak = vSite3; %obj.potentialData(idxP_R);                           
%                         for j = idxP_R:-1:iE
%                             if obj.filteredCurrentData(j) <= iP2 %filteredCurrentDataAdjusted
%                                 idxHalf = j;
%                                 break;
%                             end
%                         end
%                         vHalf = obj.potentialData(idxHalf);
%                         
%                         sigma1 = abs(vPeak - vHalf)/1.177;
%                         sigma6 = 6.0 * sigma1;                            
%                         endV = vPeak + sigma6;
% 
%                         idxEnd = -1;
%                         for j = idxP_R:-1:iE
%                             if abs(obj.potentialData(j)) <= abs(endV)
%                                 idxEnd = j;
%                                 break;
%                             else 
%                                 idxEnd = iE;
%                             end
%                         end
% %                         if idxEnd == -1
% %                             [iM,idxEnd] = min(obj.filteredCurrentData(iS:-1:iE));
% %                             idxEnd = idxEnd + iE;
% %                         end
% 
%                         q0 = sqrt(2 * pi) * sigma1 * (iP);
%                         dx = abs(vPeak - endV); % obj.potentialData(idxEnd)
%                         aRHS = erf(dx /(sqrt(2) * sigma1)); 
%                         qT = (q0 * aRHS); % Only half the area under the full Gaussian!
% 
%                         for j = 1:length(qRatio)
%                             if j < idxEnd
%                                 qRatio(j) = 0.0;
%                             elseif j >= idxEnd && j <= idxP_R
%                                 v = obj.potentialData(j);
%                                 dx = abs(vPeak - v);
%                                 lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
%                                 qVal = (q0/2.0) * lVal;
%                                 qRatio(j) = qVal/qT;
%                             elseif j > idxP_R && j < idxP_R + 2000
%                                 v = obj.potentialData(j);
%                                 dx = abs(vPeak - v);
%                                 lVal = erf(dx/(sqrt(2) * sigma1)); %1 - 
%                                 qVal = (q0/2.0) * (1.0 + lVal);   
%                                 qRatio(j) = qVal/qT;
%                             end                                    
%                         end                
%                         figure(402)
%                         hold on
%                         plot(obj.potentialData, qRatio, '-b','LineWidth',3)
%                         xlabel('Potential (V_{SCE})', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
%                         ylabel('Normalized i_{P}', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
%                         ax = gca;          
%                         ax.FontSize = obj.tick_label_size;
%                         ax.FontWeight = obj.font_weight;
%                         ax.LineWidth = obj.axis_line_width;
%                         box on
%                         hold off                            
%                         obj.filteredCurrentDataAdjusted = zeros(size(obj.filteredCurrentData));
%                         for j = 1:length(obj.filteredCurrentData)
%                             if obj.filteredCurrentData(j) >=  nC1(j)
%                                 obj.filteredCurrentDataAdjusted(j) = abs(obj.filteredCurrentData(j) - nC1(j));
%                             else
%                                 obj.filteredCurrentDataAdjusted(j) = abs(nC1(j) - obj.filteredCurrentData(j));
%                             end
%                         end                                
                    end                   
%                 case reactionNames.Fe_Red
%                     [~,indexOfMinCurrent] = min(abs(obj.filteredCurrentData));  
%                     NeedHighPotGauss = false; %true;
%                     idXStartGauss = -1;
%                     for j = indexOfMinCurrent:N
%                         diff = abs(abs(obj.potentialData(j)) - abs(vSite1));
%                         if diff <= 1.0e-2
%                             idXStartGauss = j;
%                             break;
%                         end
%                     end                
%                     if idXStartGauss == -1
%                         NeedHighPotGauss = false;
%                         idXStartGauss = indexOfMinCurrent;
%                     end
%                     if NeedHighPotGauss == true
%                         iS = indexOfMinCurrent; %idXStartGauss
%                         iE = idXStartGauss; %N;
%                         iP = obj.filteredCurrentData(iE); %obj.filteredCurrentData(iS); %Adjusted
%                         idxP_L = iS;
%                         vPeak = vSite1;
%                         iP2 = iP/2.0;
%                         
%                         for j = idxP_L:iE
%                             if obj.filteredCurrentData(j) >= iP2 %>= (iP + iP2) %Adjusted
%                                 idxHalf = j;
%                                 break;
%                             end
%                         end
%                         vHalf = obj.potentialData(idxHalf);
%                         
%                         sigma1 = abs((abs(vPeak) - abs(vHalf))/1.177);
%                         sigma6 = 6.0 * sigma1;                            
%                         endV = vPeak - sigma6;
%                         if endV <= obj.potentialData(indexOfMinCurrent)
%                             endV = obj.potentialData(indexOfMinCurrent);
%                         end
% 
%                         idxEnd = -1;
%                         for j = idxP_L:iE
%                             if abs(obj.potentialData(j)) >= abs(endV)
%                                 idxEnd = j;
%                                 break;
%                             end
%                         end
%                         if idxEnd == -1
%                             [iM,idxEnd] = min(obj.filteredCurrentDataAdjusted(idxP_L:iE));
%                             idxEnd = idxEnd + idxP_L;
%                         end
% 
%                         q0 = sqrt(2 * pi) * sigma1 * (iP);
%                         dx = abs(vPeak - endV);
%                         aLHS = erf(dx /(sqrt(2) * sigma1)); 
%                         qT = (q0 * aLHS); % Only half the area under the full Gaussian!
% 
%                         for j = 1:length(qRatio)
%                             if j < indexOfMinCurrent
%                                 qRatio(j) = 0.0;
%                             elseif j >= indexOfMinCurrent && j <= idxEnd
%                                 v = obj.potentialData(j);
%                                 dx = abs(vPeak - v);
%                                 lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
%                                 qVal = (q0/2.0) * lVal;
%                                 qRatio(j) = qVal/qT;                                            
%                             elseif j > idxEnd && j <= idxP_L
%                                 v = obj.potentialData(j);
%                                 dx = abs(vPeak - v);
%                                 lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
%                                 qVal = (q0/2.0) * lVal;
%                                 qRatio(j) = qVal/qT; 
%                             elseif j > idxP_L && j <= iE
%                                 v = obj.potentialData(j);
%                                 dx = abs(vPeak - v);
%                                 lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
%                                 qVal = (q0/2.0) * lVal;
%                                 qRatio(j) = qVal/qT; 
%                             elseif j > iE
%                                 v = obj.potentialData(j);
%                                 dx = abs(vPeak - v);
%                                 lVal = erf(dx/(sqrt(2) * sigma1)); %1 - 
%                                 qVal = (q0/2.0) * (1.0 + lVal); 
%                                 qRatio(j) = qVal/qT;                                            
%                             end                                        
%                             if abs(qRatio(j) - 0.99) <= 1.0e-2 || abs(qRatio(j) - 1.01) <= 1.0e-2
%                                 pointsToExtend = j;
%                                 break;
%                             else
%                                 pointsToExtend = length(qRatio);
%                             end
%                         end                                  
%                     else
%                         for j = 1:length(qRatio)
%                             qRatio(j) = 1.0;
%                         end
%                     end
%                     %=============================
% %                                 halfNum = round(length(qRatio)/2.0);
% %                                 figure(400)
% %                                 hold on
% %                                 plot(obj.potentialData, qRatio, '-b','LineWidth',3) %(halfNum:-1:1)
% %                                 xlabel('Potential (V_{SCE})', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
% %                                 ylabel('Surface Coverage', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
% %                                 axis square
% %                                 ylim([0.0, 1.0])                            
% %                                 ax = gca;
% %     %                             ax.XAxisLocation = 'bottom';
% %     %                             ax.YAxisLocation = 'origin';
% %                                 ax.FontSize = obj.tick_label_size;
% %                                 ax.FontWeight = obj.font_weight;
% %                                 ax.LineWidth = obj.axis_line_width;
% %                                 box on
% %                                 hold off
% 
%                     % =============================   
%                     % Assume low cathodic potentials don't allow
%                     % ORR to occur at all activation sites
%                     % =============================
%                     NeedLowPotGauss = true; %false; %
%                     if NeedHighPotGauss == false
%                         pointsToExtend = indexOfMinCurrent;
%                     end
%                     for j = 1:N
%                         diff = abs(abs(obj.potentialData(j)) - abs(vSite2));
%                         if diff <= 1.0e-4
%                             idXStartGauss = j;
%                             break;
%                         end
%                     end      
%                     iS = idXStartGauss; %obj.indicesForFitRegions(6);
%                     iE = N;
%                     iP = obj.filteredCurrentData(iS); %filteredCurrentDataAdjusted
%                     iP2 = iP/2.0;
%                     idxP_R = iS; % 
%                     
%                     if NeedLowPotGauss == true
%                         vPeak = vSite2; %obj.potentialData(idxP_R);                           
%                         for j = idxP_R:iE
%                             if obj.filteredCurrentData(j) >= (iP + iP2) %filteredCurrentDataAdjusted
%                                 idxHalf = j;
%                                 break;
%                             end
%                         end
%                         vHalf = obj.potentialData(idxHalf);
%                         
%                         sigma1 = abs(vPeak - vHalf)/1.177;
%                         sigma6 = 6.0 * sigma1;                            
%                         endV = vPeak + sigma6;
% 
%                         idxEnd = -1;
%                         for j = idxP_R:-1:iE
%                             if abs(obj.potentialData(j)) <= abs(endV)
%                                 idxEnd = j;
%                                 break;
%                             end
%                         end
%                         if idxEnd == -1
%                             [iM,idxEnd] = min(obj.filteredCurrentData(iS:iE));
%                             idxEnd = idxEnd + iE;
%                         end
% 
%                         q0 = sqrt(2 * pi) * sigma1 * (iP);
%                         dx = abs(vPeak - endV); % obj.potentialData(idxEnd)
%                         aRHS = erf(dx /(sqrt(2) * sigma1)); 
%                         qT = (q0 * aRHS); % Only half the area under the full Gaussian!
% 
%                         for j = 1:length(qRatio)
%                             if j >= pointsToExtend && j <= idxP_R %idxP_L+
%                                 v = obj.potentialData(j);
%                                 dx = abs(vPeak - v);
%                                 lVal = erf(dx/(sqrt(2) * sigma1)); %1 - 
%                                 qVal = (q0/2.0) * (1.0 + lVal);   
%                                 qRatio(j) = qVal/qT;
%                             elseif j > idxP_R %&& j < idxP_R + 2000
%                                 v = obj.potentialData(j);
%                                 dx = abs(vPeak - v);
%                                 lVal = 1.0 - erf(dx/(sqrt(2) * sigma1)); %1 - 
%                                 qVal = (q0/2.0) * lVal;
%                                 qRatio(j) = qVal/qT;
%                             end    
%                             
%                         end                                  
%                     end
%                             figure(401)
%                             hold on
%                             plot(obj.potentialData, qRatio, '-b','LineWidth',3)
%                             xlabel('Potential (V_{SCE})', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
%                             ylabel('Surface Coverage', 'FontSize', obj.axis_label_size,'FontWeight',obj.font_weight)
%                             axis square
%                             ylim([0.0, 1.0])
%                             ax = gca;  
% %                             ax.XAxisLocation = 'bottom';
% %                             ax.YAxisLocation = 'origin';
%                             ax.FontSize = obj.tick_label_size;
%                             ax.FontWeight = obj.font_weight;
%                             ax.LineWidth = obj.axis_line_width;
%                             box on
%                             hold off                               
                    % =============================                         
%                 otherwise

%             end
%         end