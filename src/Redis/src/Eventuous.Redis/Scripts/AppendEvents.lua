#!lua name=append_events

local function append_events(keys, args)
  local stream_name = keys[1]
  local expected_version = tonumber(keys[2])
  local created = keys[3]

  local info
  local stream_version = 0
  if pcall( function() info = redis.call('XINFO', 'STREAM', stream_name) end ) then
    if expected_version == -1 then
      error("WrongExpectedVersion")    
    end
    stream_version = tonumber(info[2]) - 1
    if (expected_version ~= -2 and stream_version ~= expected_version) then
      error("WrongExpectedVersion")
    end
  else
    stream_version = -1
  end

  local global_position
  local items_inserted = 0

  for i=1, table.getn(args), 4 do

      local stream_position = redis.call(
        'XADD', stream_name, '*', 
        'message_id', args[i],
        'message_type', args[i+1], 
        'json_data', args[i+2], 
        'json_metadata', args[i+3],
        'created', created
      )

      global_position = redis.call(
        'XADD', '_all', '*', 
        'stream', stream_name, 
        'position', stream_position
      )

      items_inserted = items_inserted + 1

  end

  return {stream_version + items_inserted, global_position }
end
 
redis.register_function('append_events', append_events)