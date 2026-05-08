function pdeTest
    clc;
    clear all;
    dir = 'C:\Users\spolicastro\3D Objects';
    fname = 'Box';
    ext = '.stl';
    fn = fullfile(dir,strcat(fname,ext));

    model = createpde(1);
    gm = importGeometry(model,fn);
    figure(1)
    pdegplot(gm,"FaceLabels","on")

    generateMesh(model)

    figure(2)
    pdeplot3D(model)

    c = 1.0;
    specifyCoefficients(model,"m",0,"d",0,"c",c,"a",0,"f",0);
    setInitialConditions(model,0);
    applyBoundaryCondition(model,"dirichlet","Face",3,"u",1.0);
    applyBoundaryCondition(model,"dirichlet","Face",4,"u",0);
%     applyBoundaryCondition(model,"dirichlet","Face",4:6,"u",0);
    
    R = solvepde(model);
    u = R.NodalSolution;
    
    figure(3); 
    pdeplot3D(model,"ColorMapData",u);
    title("Temperature In The Box, Steady State Solution")
    xlabel("X-coordinate, meters")
    ylabel("Y-coordinate, meters")
    zlabel("Z-coordinate, meters")
    axis equal    
end