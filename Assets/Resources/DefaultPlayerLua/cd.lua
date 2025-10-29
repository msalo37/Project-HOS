function execute(args)

    if not args or #args == 0 then
        return 4
    end
    
    if os.open_folder(args[1]) then
        return 0
    else
        print("Directory not exists!")
        return 4
    end
end