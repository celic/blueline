module API
    class Base < Grape::API

        prefix :api
        format :json
    end
end
