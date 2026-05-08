function testCond
    clc;
    clear all;


    T = 25.0;
    cCl1 = 1.0e-3:0.01:1.0;
    cCl2 = 1.0e-2:0.01:1.0;

    aSoln1 = NaClSolution(cCl1,T);
    aSoln2 = NaClSolution(cCl2,T);

    figure(11)
    hold on
    plot(cCl1,aSoln1.rhoNaCl,'-bo');
    plot(cCl2,aSoln2.rhoNaCl,'-rs');
    hold off

end