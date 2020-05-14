function rd =  plot_raw_data(rd, params)


tm = (0:(1000/2000):(length(rd.time_bins)-1)*1000/2000);


figure;
plot(tm, rd.electrode1.*1000)
title('Raw Electrode1');
xlabel('time (ms)');
ylabel('EMG Amplitude (mV)')
print('-depsc', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_Elec1_%s.eps', params.name, params.date, 'entire'))
print('-djpeg', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_Elec1_%s.jpg', params.name, params.date, 'entire'))


figure;
plot(tm, rd.electrode2.*1000)
xlabel('time (ms)');
title('Raw Electrode2');
ylabel('EMG Amplitude (mV)')
print('-depsc', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_Elec2_%s.eps', params.name, params.date, 'entire'))
print('-djpeg', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_Elec2_%s.jpg', params.name, params.date, 'entire'))


calbType = {'rest', 'flex', 'extend'};
for i = 1:length(calbType)
    cur_activity = calbType{i};
    start_samp.calb.(cur_activity) = rd.calb.(cur_activity)(2); %extracting sample indices (start)
    end_samp.calb.(cur_activity) = rd.calb.(cur_activity)(3); %extracting sample indices (finish)
    %this looks at both electrodes activity during each calibration step:
    %rest, flex, and extend
    rd.(cur_activity).elec1_calb = rd.electrode1(start_samp.calb.(cur_activity):end_samp.calb.(cur_activity));
    rd.(cur_activity).elec2_calb = rd.electrode2(start_samp.calb.(cur_activity):end_samp.calb.(cur_activity));
    
    rd.(cur_activity).elec1_calb = (rd.(cur_activity).elec1_calb)*1000;
    rd.(cur_activity).elec2_calb = (rd.(cur_activity).elec2_calb)*1000;
    
    figure;
    
    subplot(2,1,1)
    tm = (0:(1000/2000):(length(rd.(cur_activity).elec1_calb)-1)*1000/2000);
    plot(tm , rd.(cur_activity).elec1_calb);
    title(sprintf('Electrode1 during %s',(cur_activity)));
    %ylim([-10 10]);
    xlabel('time (ms)');
    ylabel('EMG Amplitude (mV)')
    hold on;
    subplot(2,1,2)
    tm = (0:(1000/2000):(length(rd.(cur_activity).elec2_calb)-1)*1000/2000);
    plot(tm , rd.(cur_activity).elec2_calb);
    title(sprintf('Electrode2 during %s',(cur_activity)));
    %ylim([-10 10]);
    xlabel('time (ms)');
    ylabel('EMG Amplitude (mV)')
    
    print('-depsc', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_calibration_%s.eps', params.name, params.date, cur_activity))
    print('-djpeg', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_calibration_%s.jpg', params.name, params.date, cur_activity))
    
    
end



end