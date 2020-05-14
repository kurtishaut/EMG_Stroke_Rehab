function [rd, params] = import_data() %rd means raw data
params.name = 'ania';
params.date = 20200213;

d = dir(sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/EMG_Stroke_Rehab-master/data_export_csv/%d_ID%s',params.date, params.name));
for i = 1:length(d)
    if startsWith(d(i).name,sprintf('%d_%s_segments',params.date, params.name))
        segments = uiimport(sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/EMG_Stroke_Rehab-master/data_export_csv/%d_ID%s/%s',params.date,params.name,d(i).name));
    elseif startsWith(d(i).name,sprintf('%d_%s_time_channels',params.date, params.name))
        time_channels = uiimport(sprintf('C:/Users/Zoe Stearns/Documents/NRT_EMG_project/EMG_Stroke_Rehab-master/EMG_Stroke_Rehab-master/data_export_csv/%d_ID%s/%s',params.date,params.name,d(i).name));
    end
end

% ft.(field) is an array
% [num_samples, index_min, index_max, t_begin, t_end,...
%       duration (sec), effective_srate]
fname = sprintf('x%d_%s_segments',params.date, params.name);
rd.calb.rest = segments.(fname)(1:7);
rd.calb.flex = segments.(fname)(8:14);
rd.calb.extend = segments.(fname)(15:21);
rd.game_play = segments.(fname)(22:28);

fname = sprintf('x%d_%s_time_channels',params.date, params.name);
rd.time_bins = time_channels.(fname)(1,:); %in ms
rd.electrode1 = time_channels.(fname)(2,:);
rd.electrode2 = time_channels.(fname)(3,:);

end