local Success = 0
local Failure = 1
local Unavailable = 2
local Retry = 3
local Pause = 6
local Resumed = 7

CCRequest = {
    id = 0,
    code = "",
    parameters = {},
    viewer = {},
    cost = 0,
    duration = 0,
    requestType = 1
}

function CCRequest:new(o)
    o = o or {}
    setmetatable(o,self)
    self.__index = self
    return o
end

function CCRequest:from_json(jstr)
    local o = json.load_string(jstr)
    local k = CCRequest:new()
    k.id = o["id"]
    k.code = o["code"]
    k.parameters = o["parameters"]
    k.viewer = o["viewer"]
    k.cost = o["cost"]
    k.requestType = o["type"]
    k.duration = o["duration"]
    return k
end

CCResponse = {
    id = 0,
    status = Retry,
    message = "Response from lua",
    timeRemaining = 0,
    responseType = 0
}

function CCResponse:new(o)
    o = o or {}
    setmetatable(o,self)
    self.__index = self
    return o
end

function CCResponse:to_json(o)
    if o == nil then
        print("o is nil")
        return ""
    end
    k = {}
    k["id"] = o.id
    k["status"] = o.status
    k["message"] = o.message
    k["timeRemaining"] = o.timeRemaining
    k["type"] = o.responseType
    return json.dump_string(k)
end
