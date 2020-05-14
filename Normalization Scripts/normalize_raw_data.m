function fd = normalize_raw_data(rd, params) % fd means filtered data

%% calculate "linear envelope" with a zero-phase Butterworth filter
gameplay_elec1 = rd.electrode1(rd.game_play(2):rd.game_play(3));
gameplay_elec2 = rd.electrode2(rd.game_play(2):rd.game_play(3));

avg_1 = mean(gameplay_elec1); 
avg_2 = mean(gameplay_elec2);

for i = 1:length(gameplay_elec1)
    fd.gameplay_envelope1(i) = abs(gameplay_elec1(i) - avg_1)*1000; %mV
end
for i = 1:length(gameplay_elec2)
    fd.gameplay_envelope2(i) = abs(gameplay_elec2(i) - avg_2)*1000; %mV
end
figure;

subplot(2,1,1);
tm = (0:(1/2000):(length(fd.gameplay_envelope1)-1)*1/2000);
plot(tm, fd.gameplay_envelope1)
title('Gameplay: Linear Envelope Electrode1');
xlabel('time (s)');
ylabel('EMG Amplitude (mV)');
%print('-depsc', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_linenvelope_elec1.eps', params.name, params.date))
%print('-djpeg', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_linenvelope_elec1.jpg', params.name, params.date))

subplot(2,1,2)
tm = (0:(1/2000):(length(fd.gameplay_envelope2)-1)*1/2000);
plot(tm, fd.gameplay_envelope2)
title('Gameplay: Linear Envelope Electrode2');
xlabel('time (s)');
ylabel('EMG Amplitude (mV)');
print('-depsc', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_linenvelope_gameplay.eps', params.name, params.date))
print('-djpeg', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_linenvelope_gameplay.jpg', params.name, params.date))
    
%%

%% normalize EMG amplitude using peak from calibration signals
bias_elec1 = max(rd.rest.elec1_calb); 
dmvc_elec1 = max(rd.flex.elec1_calb); %electrode 1 measures flexor

bias_elec2 = max(rd.rest.elec2_calb);
dmvc_elec2 = max(rd.extend.elec2_calb); %electrode 2 measures extensor


for i = 1:length(fd.gameplay_envelope1)
    fd.norm.electrode1(i) = (fd.gameplay_envelope1(i) - bias_elec1)/(dmvc_elec1 - bias_elec1); %using linear envelope
end
for i = 1:length(fd.gameplay_envelope2)
    fd.norm.electrode2(i) = (fd.gameplay_envelope2(i) - bias_elec2)/(dmvc_elec2 - bias_elec2); %using linear envelope
end
figure;

subplot(2,1,1);
tm = (0:(1/2000):(length(fd.norm.electrode1)-1)*1/2000);
plot(tm, fd.norm.electrode1)
title('Gameplay: Normalized Electrode1');
xlabel('time (s)');
ylabel('EMG Amplitude (mV)');
hold on;
%print('-depsc', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_norm_elec1.eps', params.name, params.date))
%print('-djpeg', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_norm_elec1.jpg', params.name, params.date))

subplot(2,1,2);
tm = (0:(1/2000):(length(fd.norm.electrode2)-1)*1/2000);
plot(tm, fd.norm.electrode2)
title('Gameplay: Normalized Electrode2');
xlabel('time (s)');
ylabel('EMG Amplitude (mV)');
print('-depsc', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_dmvcnorm_gameplay.eps', params.name, params.date))
print('-djpeg', sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/Figures/%s_%d_dmvcnorm_gameplay.jpg', params.name, params.date))

end
