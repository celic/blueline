module API
	class Standings < Grape::API


		resource :standings do

			desc 'Sort by'
			params do
				optional :division, type: Boolean, default: true
				optional :conference, type: Boolean, default: false
				optional :league, type: Boolean, default: false

				exactly_one_of :division, :conference, :league
			end
			get do

				team_list = []
				if params[:division]

					# get metro teams
					metro_teams = Team.all.division Enums::Division::METRO
					metro_team_ranks = metro_teams.all.select('teams.*, (wins - so) as row').order 'points desc, row desc'

					# get atlantic teams
					atlantic_teams = Team.all.division Enums::Division::ATLANTIC
					atlantic_team_ranks = atlantic_teams.all.select('teams.*, (wins - so) as row').order 'points desc, row desc'

					# get pacific teams
					pacific_teams = Team.all.division Enums::Division::PACIFIC
					pacific_team_ranks = pacific_teams.all.select('teams.*, (wins - so) as row').order 'points desc, row desc'

					# get central teams
					central_teams = Team.all.division Enums::Division::CENTRAL
					central_team_ranks = central_teams.all.select('teams.*, (wins - so) as row').order 'points desc, row desc'

					# present payload
					present metro_team_ranks
					present atlantic_team_ranks
					present pacific_team_ranks
					present central_team_ranks

				elsif params[:conference]

					# get east teams
					east_teams = Team.all.division Enums::Division::EAST
					east_team_ranks = east_teams.all.select('teams.*, (wins - so) as row').order 'points desc, row desc'

					# get west teams
					west_teams = Team.all.division Enums::Division::WEST
					west_team_ranks = west_teams.all.select('teams.*, (wins - so) as row').order 'points desc, row desc'

					# present payload
					present east_team_ranks
					present west_team_ranks

				elsif params[:league]

					# get all teams
					teams = Team.all.select('teams.*, (wins - so) as row').order 'points desc, row desc'

					# present payload
					present teams
				end
			end
		end
	end
end
