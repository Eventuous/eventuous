#!lua name=append_events

-- Entry IDs are assigned explicitly as '<milliseconds>-0' with the millisecond part bumped past
-- the last entry when needed. Auto-generated IDs ('*') bump the sequence part instead, and the
-- client-side position encoding can only represent sequence numbers 0-9.
local function last_id_ms(key)
  local entries = redis.call('XREVRANGE', key, '+', '-', 'COUNT', 1)
  if #entries == 0 then
    return 0
  end
  local id = entries[1][1]
  return tonumber(string.sub(id, 1, string.find(id, '-', 1, true) - 1))
end

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

  local time = redis.call('TIME')
  local now_ms = tonumber(time[1]) * 1000 + math.floor(tonumber(time[2]) / 1000)
  local stream_ms = last_id_ms(stream_name)
  local all_ms = last_id_ms('_all')

  for i=1, table.getn(args), 4 do

      stream_ms = math.max(now_ms, stream_ms + 1)

      local stream_position = redis.call(
        'XADD', stream_name, string.format('%.0f', stream_ms) .. '-0',
        'message_id', args[i],
        'message_type', args[i+1],
        'json_data', args[i+2],
        'json_metadata', args[i+3],
        'created', created
      )

      all_ms = math.max(now_ms, all_ms + 1)

      global_position = redis.call(
        'XADD', '_all', string.format('%.0f', all_ms) .. '-0',
        'stream', stream_name,
        'position', stream_position
      )

      items_inserted = items_inserted + 1

  end

  return {stream_version + items_inserted, global_position }
end
 
redis.register_function('append_events', append_events)