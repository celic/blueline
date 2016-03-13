module API
  class Standings < Grape::API

    represent Team, with: API::Entities::Team

    resource :standings do

      desc 'Get standings'
      params do
        optional :division, type: Boolean
        optional :conference, type: Boolean
        optional :league, type: Boolean

        exactly_one_of :division, :conference, :league
      end
      get do

        if params[:division]

          # get teams
          metro = Team.division(Enums::Division::METRO).standings
          atlantic = Team.division(Enums::Division::ATLANTIC).standings
          pacific = Team.division(Enums::Division::PACIFIC).standings
          central = Team.division(Enums::Division::CENTRAL).standings

          # present payload
          present :metro, metro
          present :atlantic, atlantic
          present :pacific, pacific
          present :central, central

        elsif params[:conference]

          # get teams
          east = Team.division(Enums::Division::EAST).standings
          west = Team.division(Enums::Division::WEST).standings

          # present payload
          present :east, east
          present :west, west

        elsif params[:league]

          # present payload
          present :teams, Team.standings
        end
      end
    end
  end
end
