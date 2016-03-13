module API
  class Base < Grape::API

    prefix :api
    format :json

    before do
      header['Access-Control-Allow-Origin'] = '*'
      header['Access-Control-Allow-Methods'] = 'GET, POST, PUT, DELETE, OPTIONS'
      header['Access-Control-Allow-Headers'] = 'accept, content-type'
    end

    mount Players
    mount PlayerStats
    mount Dashboard
    mount Games
    mount Teams
    mount Standings

    add_swagger_documentation hide_format: true
  end
end
