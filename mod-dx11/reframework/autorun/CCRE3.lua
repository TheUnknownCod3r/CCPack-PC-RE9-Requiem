CrowdControl = require "CCLuaBase"

--toggle showing names above enemies
local enable_names = true

local Success = 0
local Failure = 1
local Unavailable = 2
local Retry = 3
local Pause = 6
local Resumed = 7
local Stopped = 8


local invul = nil
local invultimer = -1.0
local invulid = 0

local ohko = nil
local ohkotimer = -1.0
local ohkoid = 0

local giant = nil
local gianttimer = -1.0
local giantid = 0

local tiny = nil
local tinytimer = -1.0
local tinyid = 0

local scalehold = false

local esize = nil
local esizetimer = -1.0
local esizeid = 0

local speed = nil
local speedtimer = -1.0
local speedid = 0

local FOV = 81.0
local fovtimer = -1.0
local fovid = 0
local fovSet = 0
local originalFOV = 81.0

local espeed = nil
local espeedtimer = -1.0
local espeedid = 0

local lasttime = 0

local paused = false
local spawns = true

-- Spawn tracking system
local spawned_enemies = {} -- Track all spawned enemies
local max_spawned_enemies = 30 -- Maximum number of spawned enemies
local enemy_check_interval = 5.0 -- Check enemy status every 5 seconds (reduced for faster detection)
local last_enemy_check = 0

-- Spawn rate limiting
local last_spawn_time = 0
local spawn_cooldown = 0.5 -- Reduced from 2.0 to 0.5 seconds between spawns
local max_spawn_attempts = 10 -- Increased from 5 to 10 maximum spawn attempts per cooldown period
local spawn_attempts = 0
local spawn_attempt_reset_time = 0

-- Safe SDK wrapper function
local function safe_sdk_call(func, ...)
    local success, result = pcall(func, ...)
    if not success then
        CCLog("SDK call failed: " .. tostring(result))
        return nil
    end
    return result
end

-- Safe singleton getter
local function safe_get_singleton(name)
    return safe_sdk_call(sdk.get_managed_singleton, name)
end

-- Safe field getter
local function safe_get_field(obj, field_name)
    if not obj then return nil end
    return safe_sdk_call(obj.get_field, obj, field_name)
end

-- Safe method caller
local function safe_call(obj, method_name, ...)
    if not obj then return nil end
    return safe_sdk_call(obj.call, obj, method_name, ...)
end

-- Draw name text over spawned enemies
local function draw_name()
    local success, result = pcall(function()
        -- Get player position for distance calculation
        local playman = safe_get_singleton(sdk.game_namespace("PlayerManager"))
        if not playman then
            return
        end
        
        local player = safe_call(playman, "get_CurrentPlayer")
        if not player then
            return
        end
        
        local player_transform = safe_call(player, "get_Transform")
        if not player_transform then
            return
        end
        
        local player_position = safe_call(player_transform, "get_Position")
        if not player_position then
            return
        end

        CCLog("Drawing Names")
        
        -- Draw name over each tracked enemy
        for enemy_id, enemy_data in pairs(spawned_enemies) do

            CCLog("Enemy Found")

            if enemy_data.gameobj then
                -- Get enemy position

                CCLog("GameObject Found")

                local enemy_transform = safe_call(enemy_data.gameobj, "get_Transform")
                if enemy_transform then

                    CCLog("Transform Found")

                    local enemy_position = safe_call(enemy_transform, "get_Position")
                    if enemy_position then
                        CCLog("Position Found")
                        -- Use the same approach as health bars for better positioning
                        -- Try to get head joint for more accurate positioning
                        local head_joint = nil
                        local success, result = pcall(function()
                            local enemy_transform = safe_call(enemy_data.gameobj, "get_Transform")
                            if enemy_transform then
                                -- Try different head joint names like health bars do
                                head_joint = safe_call(enemy_transform, "getJointByName", "head")
                                if not head_joint then
                                    head_joint = safe_call(enemy_transform, "getJointByName", "Head")
                                end
                                if not head_joint then
                                    head_joint = safe_call(enemy_transform, "getJointByName", "mouthHead")
                                end
                                if not head_joint then
                                    head_joint = safe_call(enemy_transform, "getJointByName", "root")
                                end
                            end
                        end)
                        
                        -- Use head joint position if available, otherwise fall back to transform
                        local text_position = nil
                        if head_joint then
                            CCLog("Joint Found")
                            local head_position = safe_call(head_joint, "get_Position")
                            if head_position then
                                -- Use same offset as health bars
                                local world_offset = Vector3f.new(0, 0.35, 0)
                                text_position = draw.world_to_screen(head_position + world_offset)
                            end
                        end
                        
                        -- Fallback to transform position
                        if not text_position then
                            local world_offset = Vector3f.new(0, 0.35, 0)
                            text_position = draw.world_to_screen(enemy_position + world_offset)
                        end
                        
                        if text_position then
                            -- Calculate distance for opacity
                            local distance = (player_position - enemy_position):length()
                            local opacity_scale = 1
                            if distance > 50 then
                                opacity_scale = math.max(0.3, 1 - (distance - 50) / 50)
                            end
                            
                            -- Draw name text (white with shadow) - use player name from request
                            local text = enemy_data.player_name or "JAKU" -- Use player name if available, fallback to JAKU
                            local color = 0xFFFFFFFF -- White color (ARGB format)
                            local shadow_color = 0xFF000000 -- Black shadow
                            
                            CCLog("drawing text" .. text)

                            -- Draw shadow first (behind the text)
                            draw.text(text, text_position.x + 2, text_position.y + 2, shadow_color)
                            
                            -- Draw main text (white) on top
                            draw.text(text, text_position.x, text_position.y, color)
                        end
                    end
                end
            end
        end
    end)
end


local function get_spawn_rate_limit_status()
    local current_time = os.time()
    local time_since_last_spawn = current_time - last_spawn_time
    local time_since_reset = current_time - spawn_attempt_reset_time
    
    return {
        time_since_last_spawn = time_since_last_spawn,
        time_since_reset = time_since_reset,
        spawn_attempts = spawn_attempts,
        max_attempts = max_spawn_attempts,
        cooldown = spawn_cooldown,
        can_spawn = time_since_last_spawn >= spawn_cooldown and spawn_attempts < max_spawn_attempts
    }
end

local function check_spawn_rate_limit()
    local current_time = os.time()
    
    -- Reset spawn attempts if enough time has passed
    if current_time - spawn_attempt_reset_time > spawn_cooldown then
        spawn_attempts = 0
        spawn_attempt_reset_time = current_time
        CCLog("Spawn rate limit reset - cooldown period passed")
    end
    
    -- Check if we're within cooldown period
    if current_time - last_spawn_time < spawn_cooldown then
        spawn_attempts = spawn_attempts + 1
        if spawn_attempts > max_spawn_attempts then
            CCLog("Spawn rate limit exceeded: " .. tostring(spawn_attempts) .. " attempts in " .. tostring(spawn_cooldown) .. " seconds")
            return false
        end
        CCLog("Spawn rate limited: " .. tostring(spawn_attempts) .. "/" .. tostring(max_spawn_attempts) .. " attempts")
        return false
    end
    
    -- Allow spawn and reset timer
    last_spawn_time = current_time
    spawn_attempts = 1
    spawn_attempt_reset_time = current_time
    CCLog("Spawn rate limit check passed - allowing spawn")
    return true
end

local healing = {
    spray = 1,
    herbg = 2,
    herbr = 3,
    herbb = 4,
    herbgg = 5,
    herbgr = 6,
    herbgb = 7,
    herbggb = 8,
    herbggg = 9,
    herbgrb = 10,
    herbrb = 11,    
}

local healup = {
    [2] = 5,
    [3] = 6,
    [4] = 7,
    [5] = 9,
    [6] = 10,
    [7] = 10,
    [11] = 10
}

local healdown = {
    [1] = 5,
    [5] = 2,
    [6] = 3,
    [7] = 4,
    [9] = 5,
    [10] = 6,
    [11] = 3,
    [8] = 7
}

local ammo = {
    handgun = 31,
    shotgun = 32,
    submachine = 33,
    mag = 34,
    mine = 36,--mine rounds
    explode = 37,--explosive rounds
    acid = 38,--acid rounds
    flame = 37,--flame rounds
    needle = 24,--needle cartridges
    fuel = 25,
    large = 26,--large caliber handgun
    slshigh = 27,--high powered sls 60
    detonator = 31,
    ink = 32, --ink ribbon
    board = 33, --wooden boards
   
}

local weapon = {
    g19 = 1,
    burst = 2,
    g18 = 3,
    edge = 4,
    mup = 7,
    m3 = 11,
    cqbr = 22,
    lightning = 31,
    raiden = 32,
    mgl = 42,
    knife = 46,
    survive = 47,
    hot = 48,
    rocket = 49,
    grenade = 65,
    flash = 66,
}

local weaponammo = {
    g19 = 31,
    burst = 31,
    g18 = 31,
    edge = 31,
    mup = 0,
    m3 = 32,
    cqbr = 33,
    lightning = 34,
    raiden = 0,
    mgl = 38,
    knife = 0,
    survive = 0,
    hot = 0,
    rocket = 0,
    grenade = 0,
    flash = 0,
}

local weaponbig = {
    g19 = false,
    burst = false,
    g18 = false,
    edge = false,
    mup = false,
    m3 = false,
    cqbr = true,
    lightning = false,
    raiden = true,
    mgl = false,
    knife = false,
    survive = false,
    hot = false,
    rocket = true,
    grenade = false,
    flash = false,
}



function string:split(sep)
    local result = {}
    local regex = ("([^%s]+)"):format(sep)
    for each in self:gmatch(regex) do
       table.insert(result, each)
    end
    return result
 end

function string:contains(sub)
    return self:find(sub, 1, true) ~= nil
end

function string:startswith(start)
    return self:sub(1, #start) == start
end

function string:endswith(ending)
    return ending == "" or self:sub(-#ending) == ending
end

function string:replace(old, new)
    local s = self
    local search_start_idx = 1

    while true do
        local start_idx, end_idx = s:find(old, search_start_idx, true)
        if (not start_idx) then
            break
        end

        local postfix = s:sub(end_idx + 1)
        s = s:sub(1, (start_idx - 1)) .. new .. postfix

        search_start_idx = -1 * postfix:len()
    end

    return s
end

function string:insert(pos, text)
    return self:sub(1, pos - 1) .. text .. self:sub(pos)
end

local function get_enumerator(managed_object)
	local output = {}
	managed_object = managed_object:call("GetEnumerator") or managed_object
	local name = managed_object:get_type_definition():get_full_name()
	
	if name:find(">d") or name:find("Dictionary") or name:find("erable") then 
		--managed_object:call(".ctor", 0)
		while managed_object:call("MoveNext") do 
			local current = managed_object:call("get_Current")
			if current then 
				output[current:call("get_Key")] = current:call("get_Value")
			end
		end
	else
		while managed_object:call("MoveNext") do 
			table.insert(output, managed_object:get_field("mCurrent"))
		end
	end
	return output
end


-- Spawn tracking and cleanup functions (defined early to avoid nil errors)
local function cleanup_dead_enemies()
    local current_time = os.time()
    if current_time - last_enemy_check < enemy_check_interval then
        return
    end
    
    last_enemy_check = current_time
    local alive_count = 0
    local dead_count = 0
    
    --CCLog("=== CLEANUP START ===")
    -- Count tracked enemies properly
    local tracked_count = 0
    for _, _ in pairs(spawned_enemies) do
        tracked_count = tracked_count + 1
    end
    --CCLog("Checking " .. tostring(tracked_count) .. " tracked enemies")
    
    for enemy_id, enemy_data in pairs(spawned_enemies) do
        if enemy_data.gameobj then
            -- Simple approach: try to access the object, if it fails, it's destroyed
            local success, result = pcall(function()
                -- Try to get the object's name - if this fails, object is destroyed
                local obj_name = safe_call(enemy_data.gameobj, "get_Name")
                if not obj_name then
                    dead_count = dead_count + 1
                    spawned_enemies[enemy_id] = nil
                    --CCLog("Removed destroyed enemy (no name): " .. tostring(enemy_data.name))
                    return
                end
                
                -- Try to get transform - if this fails, object is destroyed
                local transform = safe_call(enemy_data.gameobj, "get_Transform")
                if not transform then
                    dead_count = dead_count + 1
                    spawned_enemies[enemy_id] = nil
                    --CCLog("Removed destroyed enemy (no transform): " .. tostring(enemy_data.name))
                    return
                end
                
                -- If we get here, object exists - check health using proper method
                if enemy_data.enemy_controller then
                    -- Use EnemyController to get HitPoint like the health bars mod
                    local hit_point_controller = safe_call(enemy_data.enemy_controller, "get_HitPoint")
                    if hit_point_controller then
                        local current_hp = safe_call(hit_point_controller, "get_CurrentHitPoint")
                        local is_dead = safe_call(hit_point_controller, "get_IsDead")
                        
                        if current_hp and current_hp > 0 and not is_dead then
                            alive_count = alive_count + 1
                            enemy_data.last_seen = current_time
                            --CCLog("Enemy alive: " .. tostring(enemy_data.name) .. " (HP: " .. tostring(current_hp) .. ")")
                        else
                            dead_count = dead_count + 1
                            spawned_enemies[enemy_id] = nil
                            --CCLog("Removed dead enemy (HP=0 or IsDead=true): " .. tostring(enemy_data.name))
                        end
                    else
                        -- No hit point controller - enemy is likely dead/destroyed
                        dead_count = dead_count + 1
                        spawned_enemies[enemy_id] = nil
                        --CCLog("Removed enemy without hit point controller (likely dead): " .. tostring(enemy_data.name))
                    end
                else
                    -- Fallback: try direct health component check
                    local health = safe_call(enemy_data.gameobj, "getComponent(System.Type)", sdk.typeof(sdk.game_namespace("HitPoint")))
                    if health then
                        local current_hp = safe_call(health, "get_CurrentHitPoint")
                        if current_hp and current_hp > 0 then
                            alive_count = alive_count + 1
                            enemy_data.last_seen = current_time
                            --CCLog("Enemy alive (fallback): " .. tostring(enemy_data.name) .. " (HP: " .. tostring(current_hp) .. ")")
                        else
                            dead_count = dead_count + 1
                            spawned_enemies[enemy_id] = nil
                            --CCLog("Removed dead enemy (fallback HP=0): " .. tostring(enemy_data.name))
                        end
                    else
                        -- No health component - keep enemy alive for now (might be spawning)
                        alive_count = alive_count + 1
                        enemy_data.last_seen = current_time
                        --CCLog("Enemy without health component kept alive (spawning): " .. tostring(enemy_data.name))
                    end
                end
            end)
            
            if not success then
                -- Any error means the object is destroyed
                dead_count = dead_count + 1
                spawned_enemies[enemy_id] = nil
                --CCLog("Removed enemy due to error (destroyed): " .. tostring(enemy_data.name) .. " - " .. tostring(result))
            end
        else
            -- No game object reference, remove from tracking
            dead_count = dead_count + 1
            spawned_enemies[enemy_id] = nil
            --CCLog("Removed enemy with no game object: " .. tostring(enemy_data.name))
        end
    end
    
    --CCLog("=== CLEANUP END ===")
    --CCLog("Cleaned up " .. tostring(dead_count) .. " dead enemies. Alive: " .. tostring(alive_count))
    -- Count remaining enemies manually to avoid function dependency
    local remaining_count = 0
    for _, _ in pairs(spawned_enemies) do
        remaining_count = remaining_count + 1
    end
    --CCLog("Remaining tracked enemies: " .. tostring(remaining_count))
end

local function can_spawn_more_enemies()
    local current_count = 0
    for _, _ in pairs(spawned_enemies) do
        current_count = current_count + 1
    end
    
    -- If we're at the limit, force cleanup to check for dead enemies
    if current_count >= max_spawned_enemies then
        CCLog("At spawn limit (" .. tostring(current_count) .. "/" .. tostring(max_spawned_enemies) .. "), forcing cleanup")
        last_enemy_check = 0 -- Force cleanup
        cleanup_dead_enemies()
        -- Recount after cleanup
        current_count = 0
        for _, _ in pairs(spawned_enemies) do
            current_count = current_count + 1
        end
        CCLog("After forced cleanup: " .. tostring(current_count) .. "/" .. tostring(max_spawned_enemies))
    else
        -- Only cleanup every few seconds to avoid excessive calls
        local current_time = os.time()
        if current_time - last_enemy_check >= enemy_check_interval then
            cleanup_dead_enemies()
        end
    end
    
    return current_count < max_spawned_enemies
end

local function get_spawned_enemy_count()
    -- Only cleanup every few seconds to avoid excessive calls
    local current_time = os.time()
    if current_time - last_enemy_check >= enemy_check_interval then
        cleanup_dead_enemies()
    end
    
    local count = 0
    for _, _ in pairs(spawned_enemies) do
        count = count + 1
    end
    return count
end


local function add_enemy_to_tracking(enemy_name, gameobj, player_name)
    -- Generate a more unique ID using object pointer and timestamp
    local obj_ptr = tostring(gameobj):match("0x%x+") or tostring(gameobj)
    local enemy_id = obj_ptr .. "_" .. os.time() .. "_" .. math.random(1000, 9999)
    
    -- Try to get the EnemyController from the GameObject
    local enemy_controller = nil
    local success, result = pcall(function()
        -- Look for EnemyController component
        enemy_controller = safe_call(gameobj, "getComponent(System.Type)", sdk.typeof("offline.EnemyController"))
    end)
    
    if not success or not enemy_controller then
        CCLog("Warning: Could not get EnemyController for " .. enemy_name .. ", using GameObject only")
    end
    
    spawned_enemies[enemy_id] = {
        name = enemy_name,
        gameobj = gameobj,
        enemy_controller = enemy_controller,
        player_name = player_name, -- Store the player name
        spawn_time = os.time(),
        last_seen = os.time()
    }
    CCLog("Added enemy to tracking: " .. tostring(enemy_name) .. " (ID: " .. tostring(enemy_id) .. ") by " .. tostring(player_name))
    -- Count current enemies manually to avoid function dependency
    local current_count = 0
    for _, _ in pairs(spawned_enemies) do
        current_count = current_count + 1
    end
    CCLog("Current spawned enemies: " .. tostring(current_count) .. "/" .. tostring(max_spawned_enemies))
end


re.on_pre_application_entry("BeginRendering", function(element, context)
    
    local camera = sdk.get_primary_camera()
    if not camera then
        return true
    end
    
    if fovSet == 1 then 
        camera:call("set_FOV", FOV);
    end

    return true
end)



local function setOriginalFOV()
    local camera = sdk.get_primary_camera()
    if not camera then
        return true
    end
    camera:call("set_FOV", originalFOV);
end

local function getOriginalFOV()
    local camera = sdk.get_primary_camera()
    if not camera then
        return true
    end
    if fovSet == 1 then 
        originalFOV = camera:call("get_FOV");
    end
end

local function isReady()

    local flowman = sdk.get_managed_singleton("offline.gamemastering.MainFlowManager")
    if not flowman then
        CCLog('no flow manager')
        return false
    end

    -- local state = flowman:get_field("_CurrentMainState")
    -- if state ~= 6 then
    --  CCLog('state - ' .. state)
    --    return false
    --end

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))
    
    if not playman then
        return false
    end

    local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition

    if not condition then
        return false
    end

    local event = condition:get_field("<IsEvent>k__BackingField")
    if event then
        return false
    end

    local health = condition:get_field("<HitPointController>k__BackingField")
    if not health then
        return false
    end

    local hp = health:call("get_CurrentHitPoint")
    if hp <= 0 then
        return false
    end

    local guiman = sdk.get_managed_singleton(sdk.game_namespace("gui.GUIMaster"))
    if not guiman then
        return false
    end

    local open
    open = guiman:call("get_IsOpenInventory")
    if open then
        return false
    end
    open = guiman:call("get_IsOpenMap")
    if open then
        return false
    end
    open = guiman:call("get_IsOpenPause")
    if open then
        return false
    end
    open = guiman:call("get_IsOpenPauseForEvent")
    if open then
        return false
    end

    return true
end

local enemies = {}
local deferred_prefab_calls = {}
local spawned_prefabs = {}

local function spawn_deferred_prefab(packed_func_call, player_name)
	
    local scene = sdk.call_native_func(sdk.get_native_singleton("via.SceneManager"), sdk.find_type_definition("via.SceneManager"), "get_CurrentScene") 
    local spawned_prefabs_folder = scene:call("findFolder", "ModdedTemporaryObjects") or scene:call("findFolder", "RopewayGrandSceneDevelop")

    if not spawned_prefabs_folder then return nil end

	if not packed_func_call.packed_pfb or not packed_func_call.packed_pfb.prefab then 
		return nil
	end

        -- Store player name in the packed_func_call
    packed_func_call.player_name = player_name
	
	local pfb = packed_func_call.packed_pfb.prefab
	local gameobj = nil
	
	if not packed_func_call.already_exists then
		
		if not pcall(function()
			pfb:call("set_Standby", true)

            local args = packed_func_call.args

            CCLog('calling with 2+1 args out of ' .. args.n)

			gameobj = pfb:call(packed_func_call.func_name, args[1], args[2], spawned_prefabs_folder)--table.unpack(packed_func_call.args)) --spawn
		end) then 
			deferred_prefab_calls = {}
			return nil
		end
		
		if isRE2 or isRE3 then
			local guid = ValueType.new(sdk.find_type_definition("System.Guid")):call("NewGuid")
			emmgr:call("requestInstantiate", guid, packed_func_call.packed_pfb.type_id, packed_func_call.packed_pfb.name, emmgr:get_field("<LastPlayerStaySceneID>k__BackingField"), packed_func_call.args[1], packed_func_call.args[2], true, nil, nil)
			emmgr:call("execInstantiateRequests")
		end
	end
	
    if not gameobj then 
        if packed_func_call.packed_pfb.name then
            gameobj = scene:call("findGameObject(System.String)", packed_func_call.packed_pfb.name)
        end
    end

	local xform = gameobj and gameobj:call("get_Transform")
	
	if xform then 

        --local searchresults = scene:call("findComponents(System.Type)", sdk.typeof("app.ropeway.enemy.EmCommonParam"))
        --for i, result in ipairs(searchresults or {}) do
		--	local name = result:call("ToString()"):lower()
--
        --    CCLog('component found - '..name)
--
        --    pcall(sdk.call_object_func, result, "set_LoiteringEnable", true)
        --end
--
        if gameobj and packed_func_call.packed_pfb.name then
            add_enemy_to_tracking(packed_func_call.packed_pfb.name, gameobj, packed_func_call.player_name)
        end

        return true


        --local searchresults = scene:call("findComponents")
        --for i, result in ipairs(searchresults or {}) do
			--local name = result:call("ToString()"):lower()

            --CCLog('all component found - '..name)
        --end
	--	local new_spawn = spawned_prefabs[xform] or GameObject:new{xform=xform}
	--	if not isDMC then
	--		new_spawn.is_loitering=SettingsCache.loiter_by_default
	--		for i, component in ipairs(new_spawn.components) do 
	--			local name = component:call("ToString()"):lower()
	--			if name:find("em%d%d") or name:find("enemy") and not name:find("hink") then
	--				if not pcall(sdk.call_object_func, component, "awake") then 
	--					log.info(name)
	--				end
    --                CCLog('awake - '..name)
	--			end
	--			if name:find("character") then 
	--				pcall(sdk.call_object_func, component, "warp")
	--			end
	--			if name:find("em%d%d%d%dparam") then
	--				pcall(sdk.call_object_func, component, "set_LoiteringEnable", true)
	--				new_spawn.emparam = component
	--			elseif name:find("loitering") then 
	--				if SettingsCache.loiter_by_default then 
	--					pcall(sdk.call_object_func, component, "requestLoitering")
	--				end
	--				new_spawn.loitering = component
	--			end
	--		end
	--	end
	--	
	--	packed_func_call.already_exists = true
	--	packed_func_call.counter = packed_func_call.counter + 1
	--	packed_func_call.xform = xform
	--	
	--	if packed_func_call.counter == 1 then -- awake+start the components two times
	--		deferred_prefab_calls[pfb] = nil
	--	end
	--	spawned_prefabs[xform] = new_spawn
	--	return new_spawn
	end
end

local function spawn_zombie(pfb_name, pfb, folder, player_name)
	
    local scene = sdk.call_native_func(sdk.get_native_singleton("via.SceneManager"), sdk.find_type_definition("via.SceneManager"), "get_CurrentScene") 
    local spawned_prefabs_folder = scene:call("findFolder", "ModdedTemporaryObjects") or scene:call("findFolder", "RopewayGrandSceneDevelop")


	folder = folder or spawned_prefabs_folder 
	local pfb = pfb or (enemies and enemies[pfb_name] and enemies[pfb_name].prefab) 
	if not pfb then return false end
	pfb:call("set_Standby", true)
	local random_dir = Vector4f.new(math.random(-100,100)*0.01,  0.0, math.random(-100,100)*0.01, math.random(-100,100)*0.01):normalized():to_quat()
	
    local last_camera_matrix = sdk.get_primary_camera()
	if not last_camera_matrix then return false end
	last_camera_matrix = last_camera_matrix:call("get_WorldMatrix")
    
    local spawn_pos = last_camera_matrix[3] - (last_camera_matrix[2] * 2.0)

    

	
	local packed_pfb = (pfb and { prefab=pfb, name=pfb:call("get_Path"):match("^.+/(.+)%.pfb")}) or enemies[pfb_name]
	deferred_prefab_calls[pfb] = { func_name="instantiate(via.vec3, via.Quaternion, via.Folder)", args=table.pack(spawn_pos, random_dir, folder), counter=0, packed_pfb=packed_pfb }
	if pfb_name and pfb_name:find("arasite") then 
		deferred_prefab_calls[pfb].zombie = { func_name="instantiate(via.vec3, via.Quaternion, via.Folder)", args=table.pack(spawn_pos, random_dir, folder), counter=0, packed_pfb=enemies["em0000"] }
	end

    if not spawn_deferred_prefab(deferred_prefab_calls[pfb], player_name) then return false end

    return true
end

local function CCSpawn(enemy, player_name)
    local scene = sdk.call_native_func(sdk.get_native_singleton("via.SceneManager"), sdk.find_type_definition("via.SceneManager"), "get_CurrentScene") 

    if not scene then return Failure end

    if not can_spawn_more_enemies() then
        -- Count enemies manually to avoid function dependency
        local current_count = 0
        for _, _ in pairs(spawned_enemies) do
            current_count = current_count + 1
        end
        CCLog("Cannot spawn more enemies - at maximum limit: " .. tostring(current_count) .. "/" .. tostring(max_spawned_enemies))
        return Retry
    end  
    
    -- Check spawn rate limit
    if not check_spawn_rate_limit() then
        CCLog("Spawn rate limited for enemy: " .. tostring(enemy))
        return Retry
    end    
    
    local registers = scene:call("findComponents(System.Type)", sdk.typeof(sdk.game_namespace("EnemyDataManager")))
    registers = registers and registers.get_elements and registers:get_elements() or {}
    if not registers then return Failure end
    
    for i, registry in ipairs(registers) do 
        local pfb_dict = get_enumerator(registry:call("get_EnemyDataTable"))
        for type_id, pfb_register in pairs(pfb_dict) do 
            local prefab_ref = pfb_register:get_field("Prefab")
            --prefab_ref:call("set_DefaultStandby", true)
            local via_prefab = prefab_ref:get_field("PrefabField")
            local name = via_prefab:call("get_Path"):match("^.+/(.+)%.pfb")
            if not name:find("em0[678]00") then --no low-LOD zombies
                enemies[name:lower()] = { prefab=via_prefab, name=name, type_id=type_id, ref=prefab_ref }
                CCLog('enemy: ' .. name)
            end
        end
    end

    if not spawn_zombie(enemy, nil, nil, player_name) then return Retry end

    return Success
end

local function CCFixSpawn()
    local scene = sdk.call_native_func(sdk.get_native_singleton("via.SceneManager"), sdk.find_type_definition("via.SceneManager"), "get_CurrentScene") 

    if not scene then return Failure end
    
    local registers = scene:call("findComponents(System.Type)", sdk.typeof(sdk.game_namespace("EnemyDataManager")))
    registers = registers and registers.get_elements and registers:get_elements() or {}
    if not registers then return Failure end
    
    for i, registry in ipairs(registers) do 
        local pfb_dict = get_enumerator(registry:call("get_EnemyDataTable"))
        for type_id, pfb_register in pairs(pfb_dict) do 
            local prefab_ref = pfb_register:get_field("Prefab")
            prefab_ref:call("set_DefaultStandby", false) 
        end
    end

end

local function CCKill()
    CCSpawn("test")

    return Success
end

local function SetSpeed(val)
    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition

    if not condition then
        return Retry
    end

    local motion = condition:get_field("<Motion>k__BackingField")
    if not motion then
        return Retry
    end

    motion:call("set_PlaySpeed",val)
    motion:call("set_SecondaryPlaySpeed",val)


    return Success
end

local function SetEnemySpeed(val)
    local enemyman = sdk.get_managed_singleton("offline.EnemyManager")

    if not enemyman then
        return Retry
    end

    local list = enemyman:get_field("<ActiveEnemyList>k__BackingField")--enemyman:call("get_ActiveEnemyList")

    if not list then
        return Retry
    end

    local found = false

    local size = list:get_field("mSize")

    for i=0,size-1 do
        local v = list[i]

        if v then
            local cur = v:get_field("<BaseMotionSpeed>k__BackingField")

            CCLog('enemy speed: ' .. cur)

            v:set_field("<BaseMotionSpeed>k__BackingField",val)
            found = true
        end
        
    end


    if not found then
        return Retry
    end

    return Success
end

local function CCWide()
    if fovtimer > 0 then
        return retry
    end

    getOriginalFOV()
    fovSet = 1
    FOV = 130.0

    return Success
end

local function CCNarrow()
    if fovtimer > 0 then
        return retry
    end

    getOriginalFOV()
    fovSet = 1
    FOV = 50.0

    return Success
end

local function CCFast()
    if speed ~= nil then
        return retry
    end

    speed = 3.0

    return Success
end

local function CCSlow()
    if speed ~= nil then
        return retry
    end

    speed = 0.33

    return Success
end

local function CCEFast()
    if espeed ~= nil then
        return retry
    end

    espeed = 2.0

    return Success
end

local function CCESlow()
    if espeed ~= nil then
        return retry
    end

    espeed = 0.5

    return Success
end

local function CCHyper()
    if speed ~= nil then
        return retry
    end

    speed = 8.0

    return Success
end


local function CCDamage()

    if ohko ~= nil then
        return retry
    end
    if invul ~= nil then
        return retry
    end

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition

    if not condition then
        return Retry
    end

    local health = condition:get_field("<HitPointController>k__BackingField")
    if not health then
        return Retry
    end


    local hp = health:call("get_CurrentHitPoint")
    local max = health:call("get_DefaultHitPoint")

    local del = max // 4

    if hp < del / 2 then
        return Retry
    end
    
    if hp <= del then
        hp = 1
    else
        hp = hp - del
    end

    health:call("set_CurrentHitPoint", hp)
    
    return Success
end

local function CCEDamage()
    local enemyman = sdk.get_managed_singleton("offline.EnemyManager")

    if not enemyman then
        return Retry
    end

    local list = enemyman:get_field("<ActiveEnemyList>k__BackingField")--enemyman:call("get_ActiveEnemyList")

    if not list then
        return Retry
    end

    local found = false

    local size = list:get_field("mSize")
    
    for i=0,size-1 do
        local v = list[i]

        if v then

            local health = v:get_field("<HitPoint>k__BackingField")
            if not health then
                return Retry
            end


            local hp = health:call("get_CurrentHitPoint")
            local max = health:call("get_DefaultHitPoint")

            local del = max // 4

            if hp >= del / 2 then
                            
                if hp <= del then
                    hp = 1
                else
                    hp = hp - del
                end

                health:call("set_CurrentHitPoint", hp)
                found = true
            end
        end
    end
    
    if not found then
        return Retry
    end

    return Success
end

local function CCEHeal()
    local enemyman = sdk.get_managed_singleton("offline.EnemyManager")

    if not enemyman then
        return Retry
    end

    local list = enemyman:get_field("<ActiveEnemyList>k__BackingField")--enemyman:call("get_ActiveEnemyList")

    if not list then
        return Retry
    end

    local found = false

    local size = list:get_field("mSize")
    
    for i=0,size-1 do
        local v = list[i]

        if v then

            local health = v:get_field("<HitPoint>k__BackingField")
            if not health then
                return Retry
            end


            local hp = health:call("get_CurrentHitPoint")
            local max = health:call("get_DefaultHitPoint")

            local del = max // 4

            if hp <= max - del / 2 then
                    
                if hp + del > max then
                    hp = max
                else
                    hp = hp + del
                end

                health:call("set_CurrentHitPoint", hp)
                
                found = true
            end
        end
    end
    
    if not found then
        return Retry
    end

    return Success
end


local function CCEOneHP()
    local enemyman = sdk.get_managed_singleton("offline.EnemyManager")

    if not enemyman then
        return Retry
    end

    local list = enemyman:get_field("<ActiveEnemyList>k__BackingField")--enemyman:call("get_ActiveEnemyList")

    if not list then
        return Retry
    end

    local found = false

    local size = list:get_field("mSize")
    
    for i=0,size-1 do
        local v = list[i]

        if v then

            local health = v:get_field("<HitPoint>k__BackingField")
            if not health then
                return Retry
            end


            local hp = health:call("get_CurrentHitPoint")
            local max = health:call("get_DefaultHitPoint")

            if hp > 1 then
                health:call("set_CurrentHitPoint", 1)
                found = true
            end
        end
    end
    
    if not found then
        return Retry
    end

    return Success
end

local function CCEMaxHP()
    local enemyman = sdk.get_managed_singleton("offline.EnemyManager")

    if not enemyman then
        return Retry
    end

    local list = enemyman:get_field("<ActiveEnemyList>k__BackingField")--enemyman:call("get_ActiveEnemyList")

    if not list then
        return Retry
    end

    local found = false

    local size = list:get_field("mSize")
    
    for i=0,size-1 do
        local v = list[i]

        if v then

            local health = v:get_field("<HitPoint>k__BackingField")
            if not health then
                return Retry
            end


            local hp = health:call("get_CurrentHitPoint")
            local max = health:call("get_DefaultHitPoint")


            if hp < max then
                    
                health:call("set_CurrentHitPoint", max)
                
                found = true
            end
        end
    end
    
    if not found then
        return Retry
    end

    return Success
end




local function CCHeal()
    if ohko ~= nil then
        return retry
    end
    if invul ~= nil then
        return retry
    end

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition

    if not condition then
        return Retry
    end

    local health = condition:get_field("<HitPointController>k__BackingField")
    if not health then
        return Retry
    end


    local hp = health:call("get_CurrentHitPoint")
    local max = health:call("get_DefaultHitPoint")

    local del = max // 4

    if hp > max - del / 2 then
        return Retry
    end
    
    if hp + del > max then
        hp = max
    else
        hp = hp + del
    end

    health:call("set_CurrentHitPoint", hp)
    
    return Success
end

local function CCOneHP()
    if ohko ~= nil then
        return retry
    end
    if invul ~= nil then
        return retry
    end

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition

    if not condition then
        return Retry
    end

    local health = condition:get_field("<HitPointController>k__BackingField")
    if not health then
        return Retry
    end


    local hp = health:call("get_CurrentHitPoint")

    if hp < 2 then
        return Retry
    end
    
    health:call("set_CurrentHitPoint", 1)
    
    return Success
end

local function CCMaxHP()
    if ohko ~= nil then
        return retry
    end
    if invul ~= nil then
        return retry
    end

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition

    if not condition then
        return Retry
    end

    local health = condition:get_field("<HitPointController>k__BackingField")
    if not health then
        return Retry
    end


    local hp = health:call("get_CurrentHitPoint")
    local max = health:call("get_DefaultHitPoint")

    if hp >= max then
        return Retry
    end
    
    health:call("set_CurrentHitPoint", max)
    
    return Success
end

local function CCOHKO()
    if ohko ~= nil then
        return retry
    end
    if invul ~= nil then
        return retry
    end

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition

    if not condition then
        return Retry
    end

    local health = condition:get_field("<HitPointController>k__BackingField")
    if not health then
        return Retry
    end


    local hp = health:call("get_CurrentHitPoint")
    
    ohko = hp
    
    return Success
end

local function CCInvul()
    if ohko ~= nil then
        return retry
    end
    if invul ~= nil then
        return retry
    end

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition

    if not condition then
        return Retry
    end

    local health = condition:get_field("<HitPointController>k__BackingField")
    if not health then
        return Retry
    end


    local hp = health:call("get_CurrentHitPoint")
    
    invul = hp
    
    return Success
end

function CCGive()
    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local player = playman:call("get_CurrentPlayer")

    if not player then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local equipment = condition:get_field("<Equipment>k__BackingField")
    if not equipment then
        return Retry
    end

    local numslots = inventory:get_field("_CurrentSlotSize")

    CCLog("slots: " .. numslots)

    local mainslot = inventory:call("get_MainSlot")
    if not mainslot then
        return Retry
    end

    local mainslotid = mainslot:call("get_Index")
    CCLog("main slot: " .. mainslotid)

    inventory:call("unequipSlot", mainslotid)


    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end

    local slot = slots[3]
    if not slot then
        return Retry
    end

    local stock = slot:get_field("_Stock")
    if not stock then
        return Retry
    end

    local item = stock:get_field("DefaultItem")
    if not item then
        return Retry
    end

    local weap = item:get_field("WeaponId")
    if not weap then
        return Retry
    end

    CCLog("slot 0 weapon: " .. weap)

    slot:call("remove")

    slot:call("set_WeaponType", 9)
    slot:call("set_WeaponParts", 1)

    inventory:call("equipSlot", 3)

    local mainarm = equipment:get_field("<MainWeapon>k__BackingField")
    if not mainarm then
        return Retry
    end

    mainarm:call("setParts",1)
    
    return Success
end

function CCGiveHeal(item)

    CCLog("giving item: " .. item)

    local itemid = healing[item]

    CCLog("giving itemid: " .. itemid)

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local numslots = inventory:get_field("_CurrentSlotSize")

    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end


    for i=0,numslots-1 do
        local slot = slots[i]
        
        if slot then
            local empty = slot:call("get_IsBlank")

            CCLog("slot " .. i .. " empty " .. tostring(empty))

            if empty then
                CCLog("adding to slot: " .. i)
                slot:call("set_ItemID", itemid)
                return Success
            end
        end
    end

    return Retry
end

function CCGiveWeap(item)

    if item == "grenage" then item = "grenade" end

    local itemid = weapon[item]
    local big = weaponbig[item]
    local bullet = weaponammo[item]

    CCLog('item ' .. item .. ' big ' .. tostring(big))

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end


    local equipman = sdk.get_managed_singleton(sdk.game_namespace("EquipmentManager"))

    if not equipman then
        return Retry
    end

    local itemman = sdk.get_managed_singleton("offline.gamemastering.InventoryManager")

    if not itemman then
        return Retry
    end


    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local numslots = inventory:get_field("_CurrentSlotSize")

    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end

    for i=0,numslots-1 do
        local slot = slots[i]
        
        if slot then
            local empty = slot:call("get_IsBlank")
            if not empty then
                local eid = slot:call("get_WeaponType")

                if eid == itemid then

                    if item == "grenade" or item == "flash" then
                        local cur = slot:call("get_Number")
                        local max = slot:call("get_MaxNumber")
    
                        if max-cur>0 then
                            slot:call("set_Number",cur+1)
                            return Success
                        else
                            return Retry
                        end
                    else
                        return Retry
                    end
                end
            end
        end
    end

    for i=0,numslots-1 do
        local slot = slots[i]
        
        if slot then
            local empty = slot:call("get_IsBlank")

            CCLog('slot ' .. i .. ' empty ' .. tostring(empty))

            if empty then

                if big and (i==numslots - 1) then
                    return Retry
                end

                if big then 
                    local slot2 = slots[i+1]
                    empty = slot2:call("get_IsBlank")
                    if not empty then
                        return Retry
                    end
                end

                

                if big and (i==3) then
                    local slot2 = slots[i+1]
                    empty = slot2:call("get_IsBlank")
                    if not empty then
                        return Retry
                    end
                    local slot3 = slots[i+2]
                    empty = slot3:call("get_IsBlank")
                    if not empty then
                        return Retry
                    end
                    slot:call("set_ItemID", 1)
                end

                CCLog("adding to slot: " .. i)
                --slot:call("set_WeaponType", itemid)


                if item == "grenade" or item == "flash" then
                    itemman:call("addAndEquipMainWeapon", itemid, 1, bullet)
                else
                    itemman:call("addAndEquipMainWeapon", itemid, 5, bullet)
                end

                if big and (i==3) then
                    --nventory:call("exchangeSlot", 3, 4)
                    --itemman:call("exchangeStock", 3, 4)
                    --slot:call("set_Index",3)
                    slot:call("remove")
                end

                return Success
            end
        end
    end



    return Retry
end


function CCGiveAmmo(item)

    CCLog("giving item: " .. item)

    local itemid = ammo[item]

    CCLog("giving itemid: " .. itemid)

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local numslots = inventory:get_field("_CurrentSlotSize")

    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end


        
    for i=0,numslots-1 do
        local slot = slots[i]
        
        if slot then
            local empty = slot:call("get_IsBlank")

            if not empty then
                local eid = slot:call("get_ItemID")

                if eid == itemid then
                    local cur = slot:call("get_Number")
                    local max = slot:call("get_MaxNumber")

                    if max-cur>4 then
                        slot:call("set_Number",cur+5)
                        return Success
                    end

                end
            end
        end
    end

    for i=0,numslots-1 do
        local slot = slots[i]
        
        if slot then
            local empty = slot:call("get_IsBlank")

            if empty then
                slot:call("set_ItemID", itemid)
                slot:call("set_Number",5)
                return Success
            end
        end
    end

    return Retry
end

function CCHealUp()

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local numslots = inventory:get_field("_CurrentSlotSize")

    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end


    local found = false
        
    for i=0,numslots-1 do
        local slot = slots[i]
        
        if slot then
            local empty = slot:call("get_IsBlank")

            if not empty then
                local eid = slot:call("get_ItemID")

                local to = healup[eid]
                if to ~=nil then
                    found = true
                    slot:call("set_ItemID", to)
                end
            end
        end
    end

    if found then
        return Success
    end

    return Retry
end

function CCHealDown()

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local numslots = inventory:get_field("_CurrentSlotSize")

    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end


    local found = false
        
    for i=0,numslots-1 do
        local slot = slots[i]
        
        if slot then
            local empty = slot:call("get_IsBlank")

            if not empty then
                local eid = slot:call("get_ItemID")

                local to = healdown[eid]
                if to ~=nil then
                    found = true
                    slot:call("set_ItemID", to)
                end
            end
        end
    end

    if found then
        return Success
    end

    return Retry
end


function CCFillWeap()

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end


    local slot = inventory:call("get_MainSlot")
    if not slot then
        return Retry
    end
        
    local cur = slot:call("get_Number")
    local max = slot:call("get_MaxNumber")

    if cur<max then
        slot:call("set_Number",max)
        return Success
    end

    return Retry
end

function CCEmptyWeap()

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end


    local slot = inventory:call("get_MainSlot")
    if not slot then
        return Retry
    end
        
    local cur = slot:call("get_Number")

    if cur>0 then
        slot:call("set_Number",0)
        return Success
    end

    return Retry
end


function CCTakeHeal()

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local numslots = inventory:get_field("_CurrentSlotSize")

    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end


        
    for i=0,numslots-1 do
        local slot = slots[i]
        
        if slot then
            local empty = slot:call("get_IsBlank")

            if not empty then
                local eid = slot:call("get_ItemID")

                if eid > 0 and eid < 12 then
                    slot:call("remove")
                    return Success
                end
            end
        end
    end


    return Retry
end

function CCGiant()
    if giant ~= nil then
        return retry
    end
    if tiny ~= nil then
        return retry
    end

    giant = 1.5

    return Success
end

function CCTiny()
    if giant ~= nil then
        return retry
    end
    if tiny ~= nil then
        return retry
    end

    tiny = 0.33

    return Success
end

function CCEGiant()
    if esize ~= nil then
        return retry
    end

    esize = 1.5

    return Success
end

function CCETiny()
    if esize ~= nil then
        return retry
    end

    esize = 0.33

    return Success
end


function SetScale(val)
    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local head = condition:get_field("<HeadJoint>k__BackingField")
    if not head then
        return Retry
    end

    local transform = head:call("get_Owner")
    if not transform then
        return Retry
    end

    local scale = transform:call("get_LocalScale")
    if not scale then
        return Retry
    end

    scale.x = val
    scale.y = val
    scale.z = val

    transform:call("set_LocalScale", scale)

    return Success
end

function SetEnemyScale(val)
    local enemyman = sdk.get_managed_singleton("offline.EnemyManager")

    if not enemyman then
        return Retry
    end

    local list = enemyman:get_field("<ActiveEnemyList>k__BackingField")--enemyman:call("get_ActiveEnemyList")

    if not list then
        return Retry
    end

    local found = false

    local size = list:get_field("mSize")

    for i=0,size-1 do
        local v = list[i]

        if v then
            local cont = v:get_field("<EnemyCharacterController>k__BackingField")
            if  cont then

                local head = cont:get_field("<FollowJoint>k__BackingField")
                if head then
            

                    local transform = head:call("get_Owner")
                    if not transform then
                        return Retry
                    end
                
                    local scale = transform:call("get_LocalScale")
                    if not scale then
                        return Retry
                    end
                
                    scale.x = val
                    scale.y = val
                    scale.z = val
                
                    transform:call("set_LocalScale", scale)

                    found = true
                end

                local scont = cont:get_field("SubController")

                if scont then
                    head = scont:get_field("<FollowJoint>k__BackingField")
                    if head then


                        local transform = head:call("get_Owner")
                        if not transform then
                            return Retry
                        end

                    
                        local scale = transform:call("get_LocalScale")
                        if not scale then
                            return Retry
                        end
                    

                        scale.x = val
                        scale.y = val
                        scale.z = val
                    
                        transform:call("set_LocalScale", scale)

                        found = true
                    else
                        head = scont:get_field("<OwnerGameObject>k__BackingField")

                        if head then

    
                            local transform = head:call("get_Transform")
                            if not transform then
                                return Retry
                            end
    
                        
                            local scale = transform:call("get_LocalScale")
                            if not scale then
                                return Retry
                            end
                        
    
                            scale.x = val
                            scale.y = val
                            scale.z = val
                        
                            transform:call("set_LocalScale", scale)
    
                            found = true
                        end

                    end
                end

            end
        end
        
    end


    if not found then
        return Retry
    end

    return Success
end

function CCTakeAmmo()
    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local numslots = inventory:get_field("_CurrentSlotSize")

    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end


        
    for i=0,numslots-1 do
        local slot = slots[i]
        
        if slot then
            local empty = slot:call("get_IsBlank")

            if not empty then
                local eid = slot:call("get_ItemID")

                if eid > 14 and eid < 28 then

                    local cur = slot:call("get_Number")

                    if cur>5 then
                        slot:call("set_Number",cur-5)
                        return Success
                    end

                    slot:call("remove")
                    return Success
                end
            end
        end
    end


    return Retry
end

function CCTakeWeap()

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end

    local mainslot = inventory:call("get_MainSlot")
    if not mainslot then
        return Retry
    end

    local mainslotid = mainslot:call("get_Index")
    
    if mainslotid == -1 then
        return Retry
    end

    inventory:call("unequipSlot", mainslotid)
    inventory:call("removeSlot", mainslotid)
        
    CCLog("taking slot: " .. mainslotid)

    --local slot = slots[mainslotid]
    
    --slot:call("remove")

    return Success
end


function CCUnequipWeap()

    local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))

    if not playman then
        return Retry
    end

    local condition = playman:call("get_CurrentPlayerCondition")
        
    if not condition then
        return Retry
    end

    local inventory = condition:get_field("<Inventory>k__BackingField")
    if not inventory then
        return Retry
    end

    local slots = inventory:get_field("_Slots")
    if not slots then
        return Retry
    end

    local mainslot = inventory:call("get_MainSlot")
    if not mainslot then
        return Retry
    end

    local mainslotid = mainslot:call("get_Index")
    
    if mainslotid == -1 then
        return Retry
    end

    inventory:call("unequipSlot", mainslotid)
        
    
    return Success
end


local requests = {}

function CheckEffect()
    if #requests == 0 then
        return
    end

    local reqstr = table.remove(requests,1)

    local request = CCRequest:from_json(reqstr)

    --print(request)
    if request == nil then
        return
    end

    local succ, ret = pcall(
      function()
        ProcessEffect(request)
      end
    )

    if not succ then
        local response = CCResponse:new()
        response.id = request.id
        response.status = Retry
        restr = CCResponse:to_json(response)
        CCSendUpdate(restr)
        return
    end


end

function CCRunRequest()
    print(CCRequestString)
    table.insert(requests,CCRequestString)
end

function ProcessEffect(request)
    --go ahead and create a response and we can just fill it in as we go
    local response = CCResponse:new()
    --print(response)
    --now we handle the code
    response.id = request.id
    local res = Failure

    local ready = false
    if not isReady() then
        response.status = Retry
        restr = CCResponse:to_json(response)
        CCSendUpdate(restr)
        return
    end

    local code = request.code
    if code == "kill" then
        res = CCKill()
    end
    if code == "damage" then
        res = CCDamage()
    end
    if code == "heal" then
        res = CCHeal()
    end

    if code == "onehp" then
        res = CCOneHP()
    end
    if code == "full" then
        res = CCMaxHP()
    end    


    if code == "edamage" then
        res = CCEDamage()
    end
    if code == "eheal" then
        res = CCEHeal()
    end

    if code == "eonehp" then
        res = CCEOneHP()
    end
    if code == "efull" then
        res = CCEMaxHP()
    end   


    if code == "healup" then
        res = CCHealUp()
    end
    if code == "healdown" then
        res = CCHealDown()
    end

    if code == "fast" then
        res = CCFast()
        if res == Success then
            speedtimer = request.duration / 1000.0
            speedid = request.id
            response.timeRemaining = request.duration
        end
    end
    if code == "slow" then
        res = CCSlow()
        if res == Success then
            speedtimer = request.duration / 1000.0
            speedid = request.id
            response.timeRemaining = request.duration
        end
    end
    if code == "hyper" then
        res = CCHyper()
        if res == Success then
            speedtimer = request.duration / 1000.0
            speedid = request.id
            response.timeRemaining = request.duration
        end
    end

    if code == "efast" then
        res = CCEFast()
        if res == Success then
            espeedtimer = request.duration / 1000.0
            espeedid = request.id
            response.timeRemaining = request.duration
        end
    end
    if code == "eslow" then
        res = CCESlow()
        if res == Success then
            espeedtimer = request.duration / 1000.0
            espeedid = request.id
            response.timeRemaining = request.duration
        end
    end


    if code == "wide" then
        res = CCWide()
        if res == Success then
            fovtimer = request.duration / 1000.0
            fovid = request.id
            response.timeRemaining = request.duration
        end
    end
    if code == "narrow" then
        res = CCNarrow()
        if res == Success then
            fovtimer = request.duration / 1000.0
            fovid = request.id
            response.timeRemaining = request.duration
        end
    end

    if code == "give" then
        res = CCGive()
    end

    if code == "takeheal" then
        res = CCTakeHeal()
    end

    if code == "takeweap" then
        res = CCTakeWeap()
    end
    
    if code == "unequipweap" then
        res = CCUnequipWeap()
    end

    if code == "fillweap" then
        res = CCFillWeap()
    end

    if code == "emptyweap" then
        res = CCEmptyWeap()
    end

    if code == "takeammo" then
        res = CCTakeAmmo()
    end    

    if code == "giant" then
        res = CCGiant()
        if res == Success then
            gianttimer = request.duration / 1000.0
            giantid = request.id
            response.timeRemaining = request.duration
        end
    end

    if code == "tiny" then
        res = CCTiny()
        if res == Success then
            tinytimer = request.duration / 1000.0
            tinyid = request.id
            response.timeRemaining = request.duration
        end
    end

    if code == "egiant" then
        res = CCEGiant()
        if res == Success then
            esizetimer = request.duration / 1000.0
            esizeid = request.id
            response.timeRemaining = request.duration
        end
    end

    if code == "etiny" then
        res = CCETiny()
        if res == Success then
            esizetimer = request.duration / 1000.0
            esizeid = request.id
            response.timeRemaining = request.duration
        end
    end    

    if code:startswith("spawn_") then

        local items = code:split("_")

        res = CCSpawn(items[2], request.viewer)
    end 

    if code:startswith("giveheal_") then

        local items = code:split("_")

        res = CCGiveHeal(items[2])
    end

    if code:startswith("giveweap_") then

        local items = code:split("_")

        res = CCGiveWeap(items[2])
    end    

    if code:startswith("giveammo_") then

        local items = code:split("_")

        res = CCGiveAmmo(items[2])
    end 

    if code == "ohko" then
        res = CCOHKO()
        if res == Success then
            ohkotimer = request.duration / 1000.0
            ohkoid = request.id
            response.timeRemaining = request.duration
        end
    end
    if code == "invul" then
        res = CCInvul()
        if res == Success then
            invultimer = request.duration / 1000.0
            invulid = request.id
            response.timeRemaining = request.duration
        end 
    end

    response.status = res


    
    --print(response.id)
    print(response.status)
    if response.status == Success then
        --now we create a string to send the player
        --SendMessage(string.format("<COL RED>%s</COL> has sent <PL>: %s",request.viewer,CCCodeToName(code)))
    end
    restr = CCResponse:to_json(response)
    print(restr)
    CCSendUpdate(restr)
end

math.randomseed(os.time())

local function SendUpdate(id, type)
    local response = CCResponse:new()
    response.id = id
    response.status = type
    local str = CCResponse:to_json(response)

    CCSendUpdate(str)
end

local function UpdateTimers()
    
    local application = sdk.get_native_singleton("via.Application")

    if not application then
        return nil
    end
    local app_type = sdk.find_type_definition("via.Application")
    

    local time = sdk.call_native_func(application, app_type, "get_UpTimeSecond")--CurrentElapsedTimeMillisecond")

    if lasttime > 0 then
        local deltaTime = time - lasttime

        --CCLog("giant " .. gianttimer .. " delta " .. deltaTime)

        if isReady() then

            if paused then
                CCLog('unpausing effects')

                if ohkotimer > 0 then SendUpdate(ohkoid, Resumed) end
                if invultimer > 0 then SendUpdate(invulid, Resumed) end
                if tinytimer > 0 then SendUpdate(tinyid, Resumed) end
                if gianttimer > 0 then SendUpdate(giantid, Resumed) end
                if speedtimer > 0 then SendUpdate(speedid, Resumed) end
                if espeedtimer > 0 then SendUpdate(espeedid, Resumed) end
                if fovtimer > 0 then SendUpdate(fovid, Resumed) end
                if esizetimer > 0 then SendUpdate(esizeid, Resumed) end
                
                paused = false
            end

            if deltaTime < 0 then
                deltaTime = 0
            end

            if ohkotimer > 0 then
                ohkotimer = ohkotimer - deltaTime
            end
            if invultimer > 0 then
                invultimer = invultimer - deltaTime
            end  
            
            if tinytimer > 0 then
                tinytimer = tinytimer - deltaTime
            end
            if gianttimer > 0 then
                gianttimer = gianttimer - deltaTime
            end
            if speedtimer > 0 then
                speedtimer = speedtimer - deltaTime
            end
            if espeedtimer > 0 then
                espeedtimer = espeedtimer - deltaTime
            end
            if fovtimer > 0 then
                fovtimer = fovtimer - deltaTime
            end
            if esizetimer > 0 then
                esizetimer = esizetimer - deltaTime
            end
        else

            if not paused then

                if gianttimer > 0 or tinytimer > 0 then
                    SetScale(1.0)
                end

                if esizetimer > 0 then
                    SetEnemyScale(1.0)
                end

                if ohkotimer > 0 then SendUpdate(ohkoid, Pause) end
                if invultimer > 0 then SendUpdate(invulid, Pause) end
                if tinytimer > 0 then SendUpdate(tinyid, Pause) end
                if gianttimer > 0 then SendUpdate(giantid, Pause) end
                if speedtimer > 0 then SendUpdate(speedid, Pause) end
                if espeedtimer > 0 then SendUpdate(espeedid, Pause) end
                if fovtimer > 0 then SendUpdate(fovid, Pause) end
                if esizetimer > 0 then SendUpdate(esizeid, Pause) end
                
                paused = true

                scalehold = false
            end

        end
    end

    lasttime = time
end

local function on_pre_get_timescale(args)
    CCLog("Playing Event")
    if not paused then
        if gianttimer > 0 or tinytimer > 0 then
            SetScale(1.0)
            scalehold = true
        end
        if esizetimer > 0 then
            SetEnemyScale(1.0)
            scalehold = true
        end
    end
end

local function on_post_get_timescale(retval)

    return retval
end

sdk.hook(sdk.find_type_definition("offline.fsmv2.PlayEvent"):get_method("start"), on_pre_get_timescale, on_post_get_timescale)


re.on_frame(function()

    -- Periodic cleanup of dead enemies
    cleanup_dead_enemies()
    
    -- Draw name text over spawned enemies
    if enable_names then
        draw_name()
    end

    local flowman = sdk.get_managed_singleton("offline.gamemastering.MainFlowManager")
    if not flowman then
        CCLog('no flow manager')
        return
    end

    local game = flowman:call("get_IsInGame")
    local over = flowman:call("get_IsInGameOver")

    if game and not over then
        spawns = true
    else

        if spawns then
         local emmgr = sdk.get_managed_singleton(sdk.game_namespace("EnemyManager"))
         local scene = sdk.call_native_func(sdk.get_native_singleton("via.SceneManager"), sdk.find_type_definition("via.SceneManager"), "get_CurrentScene") 
         local spawned_prefabs_folder = scene:call("findFolder", "ModdedTemporaryObjects") or scene:call("findFolder", "RopewayGrandSceneDevelop")
         if spawned_prefabs_folder then
             spawned_prefabs_folder:call("deactivate") --fixes infinite loading
             spawned_prefabs_folder:call("activate")
             if emmgr then
                emmgr:call("get_ActiveEnemyList"):call("TrimExcess") 
             end
            end
        end

        spawns = false
    end
end)

re.on_pre_application_entry("UpdateBehavior", function()
    --main loop access, update timers in here
    UpdateTimers()

    if fovtimer <= 0 then
        if FOV ~= originalFOV then
            local response = CCResponse:new()
            response.id = fovid
            response.status = Stopped
            local str = CCResponse:to_json(response)
            
            CCSendUpdate(str)
            
            FOV = originalFOV
            fovSet = 0
            setOriginalFOV()
            return
        end     
    end

    if esizetimer <= 0 then
        if esize ~= nil then
            if SetEnemyScale(1.0) == Success then
                local response = CCResponse:new()
                response.id = esizeid
                response.status = Stopped
                local str = CCResponse:to_json(response)

                CCSendUpdate(str)

                esize = nil
                scalehold = false
                return
            end
        end
    else
        if esize ~= nil and not scalehold and not paused then
            SetEnemyScale(esize)
        end        
    end

    if gianttimer <= 0 then
        if giant ~= nil then
            if SetScale(1.0) == Success then
                local response = CCResponse:new()
                response.id = giantid
                response.status = Stopped
                local str = CCResponse:to_json(response)

                CCSendUpdate(str)

                giant = nil
                scalehold = false
                return
            end
        end
    else
        if giant ~= nil and not scalehold and not paused then
            SetScale(giant)
        end        
    end

    if tinytimer <= 0 then
        if tiny ~= nil then
            if SetScale(1.0) == Success then
                local response = CCResponse:new()
                response.id = tinyid
                response.status = Stopped
                local str = CCResponse:to_json(response)

                CCSendUpdate(str)

                tiny = nil
                scalehold = false
                return
            end
        end
    else
        if tiny ~= nil and not scalehold and not paused then
            SetScale(tiny)
        end        
    end

    if speedtimer <= 0 then
        if speed ~= nil then
            if SetSpeed(1.0) == Success then
                local response = CCResponse:new()
                response.id = speedid
                response.status = Stopped
                local str = CCResponse:to_json(response)

                CCSendUpdate(str)

                speed = nil
                return
            end
        end
    else
        if speed ~= nil then
            SetSpeed(speed)
        end        
    end

    if espeedtimer <= 0 then
        if espeed ~= nil then
            if SetEnemySpeed(1.0) == Success then
                local response = CCResponse:new()
                response.id = espeedid
                response.status = Stopped
                local str = CCResponse:to_json(response)

                CCSendUpdate(str)

                espeed = nil
                return
            end
        end
    else
        if espeed ~= nil then
            SetEnemySpeed(espeed)
        end        
    end

    if ohkotimer <= 0 then
        if ohko ~= nil then
            
            local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))
    
            if not playman then
                return
            end
        
            local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition
        
            if not condition then
                return
            end
        
            local health = condition:get_field("<HitPointController>k__BackingField")
            if not health then
                return
            end
        
            local hp = health:call("get_CurrentHitPoint")

            if hp > 0 then
                health:call("set_CurrentHitPoint", ohko)
                ohko = nil

                local response = CCResponse:new()
                response.id = ohkoid
                response.status = Stopped
                local str = CCResponse:to_json(response)

                CCSendUpdate(str)
                return
            end
        end
    else
        if ohko ~= nil then
            local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))
    
            if not playman then
                return
            end
        
            local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition
        
            if not condition then
                return
            end
        
            local health = condition:get_field("<HitPointController>k__BackingField")
            if not health then
                return
            end

            local hp = health:call("get_CurrentHitPoint")
        
            if hp > 0 then
                health:call("set_CurrentHitPoint", 1)
            end
        end        
    end

    if invultimer <= 0 then
        if invul ~= nil then

            local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))
    
            if not playman then
                return
            end
        
            local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition
        
            if not condition then
                return
            end
        
            local health = condition:get_field("<HitPointController>k__BackingField")
            if not health then
                return
            end

            local hp = health:call("get_CurrentHitPoint")
            if hp > 0 then
                health:call("set_CurrentHitPoint", invul)
                invul = nil

                local response = CCResponse:new()
                response.id = invulid
                response.status = Stopped
                local str = CCResponse:to_json(response)

                CCSendUpdate(str)
                return
            end
        end
    else
        if invul ~= nil then
            local playman = sdk.get_managed_singleton(sdk.game_namespace("PlayerManager"))
    
            if not playman then
                return
            end
        
            local condition = playman:call("get_CurrentPlayerCondition") --playman.CurrentPlayerCondition
        
            if not condition then
                return
            end
        
            local health = condition:get_field("<HitPointController>k__BackingField")
            if not health then
                return
            end
        
            local hp = health:call("get_CurrentHitPoint")
            local max = health:call("get_DefaultHitPoint")

            if hp > 0 then
                health:call("set_CurrentHitPoint", max)
            end

            
        end        
    end

    CheckEffect()

end)
