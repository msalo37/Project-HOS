function execute(args)

    if not args or #args == 0 then
        return 4
    end
    
    return os.create_folder(args[1])
end